using System;
using System.Collections.Generic;
using System.Windows.Forms;

sealed class SettingsForm : Form
{
    private readonly ComboBox sourceComboBox;
    private readonly ComboBox targetComboBox;
    private readonly CheckBox startWithWindowsCheckBox;

    public RoutingConfiguration? SelectedConfiguration
    {
        get;
        private set;
    }

    public bool StartWithWindows =>
        startWithWindowsCheckBox.Checked;

    public SettingsForm(
        RoutingConfiguration? currentConfiguration,
        bool startWithWindows
    )
    {
        Text =
            "IME Layout Router Settings";

        ClientSize =
            new System.Drawing.Size(
                420,
                220
            );

        FormBorderStyle =
            FormBorderStyle.FixedDialog;

        MaximizeBox = false;
        MinimizeBox = false;

        StartPosition =
            FormStartPosition.CenterScreen;

        Label sourceLabel =
            new Label
            {
                Text = "Source IME",
                Left = 20,
                Top = 20,
                Width = 150
            };

        sourceComboBox =
            new ComboBox
            {
                Left = 20,
                Top = 45,
                Width = 380,
                DropDownStyle =
                    ComboBoxStyle.DropDownList
            };

        Label targetLabel =
            new Label
            {
                Text = "Target Keyboard Layout",
                Left = 20,
                Top = 80,
                Width = 200
            };

        targetComboBox =
            new ComboBox
            {
                Left = 20,
                Top = 105,
                Width = 380,
                DropDownStyle =
                    ComboBoxStyle.DropDownList
            };

        startWithWindowsCheckBox =
            new CheckBox
            {
                Text =
                    "Start IME Layout Router with Windows",
                Left = 20,
                Top = 145,
                Width = 300,
                Checked = startWithWindows
            };

        Button saveButton =
            new Button
            {
                Text = "Save",
                Left = 240,
                Top = 180,
                Width = 75
            };

        Button cancelButton =
            new Button
            {
                Text = "Cancel",
                Left = 325,
                Top = 180,
                Width = 75
            };

        Controls.Add(sourceLabel);
        Controls.Add(sourceComboBox);
        Controls.Add(targetLabel);
        Controls.Add(targetComboBox);
        Controls.Add(startWithWindowsCheckBox);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton =
            saveButton;

        CancelButton =
            cancelButton;

        cancelButton.DialogResult =
            DialogResult.Cancel;

        var candidates =
            TsfProfileEnumerator
                .GetSelectableProfiles();

        List<InputProfile> sources =
            candidates.Sources;

        List<InputProfile> targets =
            candidates.Targets;

        sourceComboBox.DataSource =
            sources;

        sourceComboBox.DisplayMember =
            nameof(InputProfile.DisplayName);

        targetComboBox.DataSource =
            targets;

        targetComboBox.DisplayMember =
            nameof(InputProfile.DisplayName);

        if (currentConfiguration != null)
        {
            int sourceIndex =
                sources.FindIndex(
                    profile =>
                        profile.HasSameIdentityAs(
                            currentConfiguration.Source
                        )
                );

            if (sourceIndex >= 0)
            {
                sourceComboBox.SelectedIndex =
                    sourceIndex;
            }

            int targetIndex =
                targets.FindIndex(
                    profile =>
                        profile.HasSameIdentityAs(
                            currentConfiguration.Target
                        )
                );

            if (targetIndex >= 0)
            {
                targetComboBox.SelectedIndex =
                    targetIndex;
            }
        }

        saveButton.Enabled =
            sources.Count > 0
            && targets.Count > 0;

        saveButton.Click +=
            (_, _) =>
            {
                if (
                    sourceComboBox.SelectedItem
                        is not InputProfile source
                    ||
                    targetComboBox.SelectedItem
                        is not InputProfile target
                )
                {
                    return;
                }

                SelectedConfiguration =
                    new RoutingConfiguration(
                        source,
                        target
                    );

                DialogResult =
                    DialogResult.OK;

                Close();
            };
    }
}