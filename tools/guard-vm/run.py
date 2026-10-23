"""Disposable nested Windows VM; never provision on a developer machine."""
import hashlib
import http.server
import json
import os
import re
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import threading
import time
import io
import urllib.request
import zipfile
from PIL import Image

if (os.environ.get("GITHUB_ACTIONS") != "true"
        or os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted"
        or os.environ.get("GITHUB_REPOSITORY") != "31916/ImeLayoutRouter"
        or os.name != "posix" or not Path("/dev/kvm").exists()):
    raise SystemExit("Only the disposable GitHub-hosted Linux VM is permitted")

root = Path(os.environ["RUNNER_TEMP"]) / "v3-windows-vm"
root.mkdir()
evidence = Path("artifacts/vm-evidence").resolve()
evidence.mkdir(parents=True, exist_ok=True)
payload = Path("artifacts/vm").resolve()
token = secrets.token_urlsafe(32)
password = secrets.token_urlsafe(24)
phase = "preparing"
finished = threading.Event()
resume_cycles = 0

class Receiver(http.server.BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_POST(self):
        global phase, resume_cycles
        if self.headers.get("Authorization") != token:
            self.send_error(403)
            return
        length = int(self.headers.get("Content-Length", 0))
        if length < 1 or length > 20 * 1024 * 1024:
            self.send_error(413)
            return
        body = self.rfile.read(length)
        if self.path == "/status":
            phase = str(json.loads(body)["phase"])
            print("Guest phase:", phase, flush=True)
            if phase.startswith("attempt-start-"):
                resume_cycles = 0
            if phase.startswith("finished-"):
                finished.set()
        elif self.path == "/result":
            (evidence / "guest-results.zip").write_bytes(body)
        elif re.fullmatch(r"/result-[0-9a-f]{40}", self.path):
            (evidence / (self.path[1:] + ".zip")).write_bytes(body)
            # Fixed fixture logs only: expose failures while this prepared VM
            # is still available for a new source revision, preserving the zip.
            with zipfile.ZipFile(io.BytesIO(body)) as archive:
                for name in archive.namelist():
                    if name.endswith("checks.txt"):
                        lines = archive.read(name).decode("utf-8-sig", errors="replace").splitlines()
                        findings = [line for line in lines if "FAIL" in line or "Exception:" in line]
                        for line in findings[:8]:
                            print("Fixture failure:", line[:1000], flush=True)
        else:
            self.send_error(404)
            return
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"OK")

server = http.server.ThreadingHTTPServer(("127.0.0.1", 8000), Receiver)
threading.Thread(target=server.serve_forever, daemon=True).start()
(payload / "config.json").write_text(json.dumps({"endpoint": "http://10.0.2.2:8000", "token": token}))

# Fixed preinstalled SDK directories on this disposable Linux image only.
# They contain no repository data and are not needed by this VM-only job.
if shutil.disk_usage(root).free < 45 * 1024**3:
    for entry in ("/usr/local/lib/android", "/usr/share/dotnet", "/opt/ghc"):
        path = Path(entry)
        if path.exists() and path.resolve() == path:
            subprocess.run(["sudo", "rm", "--recursive", "--force", "--one-file-system", "--", entry], check=True)
if shutil.disk_usage(root).free < 35 * 1024**3:
    raise SystemExit("Insufficient disposable disk space for Windows evaluation")

# Official evaluation download and Microsoft's independently published SHA-256:
# https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise
# https://support.microsoft.com/en-us/servicing/os/windows/docs/2026/09/verify-the-authenticity-of-a-windows-11-enterprise-evaluation-iso-file
iso = root / "windows11-ja.iso"
url = "https://software-static.download.prss.microsoft.com/dbazure/26300.9457.260913-1737.26h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_ja-jp.iso"
for attempt in range(3):
    download = subprocess.Popen(["curl", "--silent", "--show-error", "--fail", "--location", "--continue-at", "-", "--max-time", "1200", "--output", str(iso), url])
    while download.poll() is None:
        try:
            download.wait(timeout=30)
        except subprocess.TimeoutExpired:
            print("Official ISO download bytes:", iso.stat().st_size if iso.exists() else 0, flush=True)
    if download.returncode == 0:
        break
    if attempt == 2:
        raise RuntimeError("Official evaluation download failed after bounded resumable attempts")
with iso.open("rb") as source:
    digest = hashlib.file_digest(source, "sha256").hexdigest().upper()
if digest != "8FB2CC19C8AEE87A688C1CB73E726816C01F0C6EE9A17BB2FB73E4533837E698":
    raise SystemExit("Official Windows evaluation ISO hash mismatch")
print("Verified official Windows 11 Japanese x64 evaluation ISO", flush=True)

# Official SPICE QXL package; only the three named x64 driver files are copied.
# The guest additionally verifies Microsoft's hardware-publisher catalog.
driver_url = "https://www.spice-space.org/download/windows/qxl-wddm-dod/qxl-wddm-dod-0.21/spice-qxl-wddm-dod-0.21-no-MSIs.zip"
with urllib.request.urlopen(driver_url, timeout=60) as response:
    driver = response.read(2 * 1024 * 1024)
if hashlib.sha256(driver).hexdigest() != "78c079c53b84cc0550841a1a8f71b6bb1fbd4caa48682cd9db132524fd9332d9":
    raise SystemExit("Pinned QXL driver archive hash mismatch")
(payload / "display-driver").mkdir()
with zipfile.ZipFile(io.BytesIO(driver)) as archive:
    for name in ("qxldod.inf", "qxldod.sys", "qxldod.cat"):
        (payload / "display-driver" / name).write_bytes(archive.read("w10/amd64/" + name))

unattend = rf'''<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend">
 <settings pass="windowsPE">
  <component name="Microsoft-Windows-International-Core-WinPE" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
   <SetupUILanguage><UILanguage>ja-JP</UILanguage></SetupUILanguage><InputLocale>0411:00000411</InputLocale><SystemLocale>ja-JP</SystemLocale><UILanguage>ja-JP</UILanguage><UserLocale>ja-JP</UserLocale>
  </component>
  <component name="Microsoft-Windows-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
   <DiskConfiguration><Disk wcm:action="add"><DiskID>0</DiskID><WillWipeDisk>true</WillWipeDisk><CreatePartitions>
    <CreatePartition wcm:action="add"><Order>1</Order><Type>EFI</Type><Size>260</Size></CreatePartition>
    <CreatePartition wcm:action="add"><Order>2</Order><Type>MSR</Type><Size>16</Size></CreatePartition>
    <CreatePartition wcm:action="add"><Order>3</Order><Type>Primary</Type><Extend>true</Extend></CreatePartition>
   </CreatePartitions><ModifyPartitions>
    <ModifyPartition wcm:action="add"><Order>1</Order><PartitionID>1</PartitionID><Format>FAT32</Format><Label>System</Label></ModifyPartition>
    <ModifyPartition wcm:action="add"><Order>2</Order><PartitionID>3</PartitionID><Format>NTFS</Format><Label>Windows</Label><Letter>C</Letter></ModifyPartition>
   </ModifyPartitions></Disk><WillShowUI>OnError</WillShowUI></DiskConfiguration>
   <ImageInstall><OSImage><InstallFrom><MetaData wcm:action="add"><Key>/IMAGE/INDEX</Key><Value>1</Value></MetaData></InstallFrom><InstallTo><DiskID>0</DiskID><PartitionID>3</PartitionID></InstallTo><WillShowUI>OnError</WillShowUI></OSImage></ImageInstall>
   <UserData><AcceptEula>true</AcceptEula><FullName>V3 disposable test</FullName><Organization>IMELayoutRouter</Organization></UserData>
  </component>
 </settings>
 <settings pass="specialize">
  <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"><ComputerName>V3-TEST</ComputerName><TimeZone>UTC</TimeZone></component>
 </settings>
 <settings pass="oobeSystem">
  <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"><InputLocale>0411:00000411</InputLocale><SystemLocale>ja-JP</SystemLocale><UILanguage>ja-JP</UILanguage><UserLocale>ja-JP</UserLocale></component>
  <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
   <OOBE><HideEULAPage>true</HideEULAPage><HideOnlineAccountScreens>true</HideOnlineAccountScreens><HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE><ProtectYourPC>3</ProtectYourPC></OOBE>
   <UserAccounts><LocalAccounts><LocalAccount wcm:action="add"><Name>V3Test</Name><Group>Administrators</Group><Password><Value>{password}</Value><PlainText>true</PlainText></Password></LocalAccount></LocalAccounts></UserAccounts>
   <AutoLogon><Enabled>true</Enabled><Username>V3Test</Username><LogonCount>2</LogonCount><Password><Value>{password}</Value><PlainText>true</PlainText></Password></AutoLogon>
   <FirstLogonCommands><SynchronousCommand wcm:action="add"><Order>1</Order><Description>Run isolated V3 checks</Description><CommandLine>powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "$d=Get-Volume | Where-Object FileSystemLabel -eq 'V3TEST' | Select-Object -First 1; &amp; ($d.DriveLetter + ':\guest.ps1')"</CommandLine></SynchronousCommand></FirstLogonCommands>
  </component>
 </settings>
</unattend>'''
(payload / "Autounattend.xml").write_text(unattend, encoding="utf-8")
payload_iso = root / "payload.iso"
subprocess.run(["genisoimage", "-quiet", "-J", "-R", "-V", "V3TEST", "-o", str(payload_iso), str(payload)], check=True)
disk = root / "windows.qcow2"
subprocess.run(["qemu-img", "create", "-f", "qcow2", str(disk), "64G"], check=True)
variables = root / "vars.fd"
shutil.copyfile("/usr/share/OVMF/OVMF_VARS_4M.ms.fd", variables)
tpm = root / "tpm"
tpm.mkdir()
subprocess.run(["swtpm", "socket", "--tpm2", "--tpmstate", f"dir={tpm}", "--ctrl", f"type=unixio,path={root}/tpm.sock", "--daemon"], check=True)
qmp_path = root / "qmp.sock"
command = ["qemu-system-x86_64", "-enable-kvm", "-machine", "q35,smm=on", "-cpu", "host", "-smp", "4", "-m", "8192",
           "-global", "driver=cfi.pflash01,property=secure,value=on",
           "-drive", "if=pflash,format=raw,readonly=on,file=/usr/share/OVMF/OVMF_CODE_4M.ms.fd",
           "-drive", f"if=pflash,format=raw,file={variables}",
           "-chardev", f"socket,id=chrtpm,path={root}/tpm.sock", "-tpmdev", "emulator,id=tpm0,chardev=chrtpm", "-device", "tpm-tis,tpmdev=tpm0",
           "-drive", f"file={disk},format=qcow2,if=ide", "-drive", f"file={iso},media=cdrom,readonly=on,if=ide",
           "-drive", f"file={payload_iso},media=cdrom,readonly=on,if=ide", "-boot", "once=d,menu=off",
           "-device", "e1000,netdev=net0", "-netdev", "user,id=net0", "-vga", "qxl",
           "-device", "qxl,id=secondary", "-spice", "unix=on,addr=" + str(root / "spice.sock") + ",disable-ticketing=on",
           "-display", "none", "-qmp", f"unix:{qmp_path},server=on,wait=off"]
with (evidence / "qemu.txt").open("w") as log:
    process = subprocess.Popen(command, stdout=log, stderr=log)
    try:
        deadline = time.monotonic() + 60
        while not qmp_path.exists() and time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError("QEMU exited during setup")
            time.sleep(1)
        connection = socket.socket(socket.AF_UNIX)
        connection.connect(str(qmp_path))
        stream = connection.makefile("rwb", buffering=0)
        json.loads(stream.readline())

        def qmp(name, arguments=None):
            stream.write((json.dumps({"execute": name, "arguments": arguments or {}}) + "\n").encode())
            while True:
                response = json.loads(stream.readline())
                if "return" in response:
                    return response["return"]
                if "error" in response:
                    raise RuntimeError(str(response["error"]))

        qmp("qmp_capabilities")
        if not qmp("query-current-machine").get("wakeup-suspend-support"):
            raise RuntimeError("The disposable machine cannot resume from ACPI sleep")
        started = time.monotonic()
        last_report = 0
        suspended_at = None
        resume_cycles = 0
        while not finished.wait(1):
            elapsed = time.monotonic() - started
            # Two virtual GPUs and Secure Boot can delay the optical boot
            # prompt beyond 35 seconds. Its acceptance window is only seconds.
            # All keys remain inside this newly created, empty disposable VM.
            if elapsed < 180:
                qmp("send-key", {"keys": [{"type": "qcode", "data": "ret"}]})
            state = qmp("query-status")["status"]
            if state == "suspended":
                if not phase.startswith("suspend-ready-"):
                    raise RuntimeError("Unexpected guest suspend outside the input lifecycle test")
                if suspended_at is None:
                    suspended_at = time.monotonic()
                    print("Observed actual guest ACPI S3", flush=True)
                elif time.monotonic() - suspended_at >= 10:
                    qmp("system_wakeup")
                    resume_cycles += 1
                    suspended_at = None
                    (evidence / "hypervisor-resume.txt").write_text(f"actual_s3_resume_cycles={resume_cycles}\n")
                    print("Resumed actual guest ACPI S3, cycle", resume_cycles, flush=True)
            if elapsed - last_report >= 60:
                last_report = elapsed
                print(f"VM {int(elapsed)}s: {phase}; {qmp('query-status')['status']}", flush=True)
                ppm = root / "screen.ppm"
                qmp("screendump", {"filename": str(ppm)})
                with Image.open(ppm) as shot:
                    shot.save(evidence / "latest-screen.png")
                # Installation errors remain inspectable while this job runs.
                ocr = subprocess.run(["tesseract", str(evidence / "latest-screen.png"), "stdout", "-l", "jpn+eng"], capture_output=True, text=True)
                print("Guest screen:", ocr.stdout.strip()[:1500], flush=True)
            if elapsed > 55 * 60:
                raise TimeoutError("Windows VM validation did not finish within its bounded window")
            if process.poll() is not None:
                raise RuntimeError("VM exited before validation completed")
        if phase != "finished-0":
            raise RuntimeError("Guest validation failed; see retained evidence")
        with zipfile.ZipFile(evidence / "guest-results.zip") as archive:
            result = archive.read("result.txt").decode("utf-8-sig").strip()
            if result != "exit=0":
                raise RuntimeError("Guest result does not confirm success")
        if resume_cycles != 2:
            raise RuntimeError("Two actual ACPI sleep/resume cycles were not verified")
        print("PASS Isolated Windows 11 x64 checks", flush=True)
    finally:
        process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
        server.shutdown()
