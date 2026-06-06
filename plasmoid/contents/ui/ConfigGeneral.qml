import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

ConfigPageBase {
    id: root

    ColumnLayout {
        width: root.availableWidth
        spacing: 12

        GroupBox {
            title: i18n("Show sections")
            Layout.fillWidth: true
            GridLayout {
                columns: 3
                anchors.fill: parent

                CheckBox { text: "Claude"; checked: root.settings ? root.settings.showClaude : false; onToggled: { root.settings.showClaude = checked; root.save() } }
                SpinBox { from: 50; to: 600; value: root.settings ? root.settings.claudeWidth : 170; editable: true; onValueModified: { root.settings.claudeWidth = value; root.save() } }
                Label { text: i18n("Width") }

                CheckBox { text: "Codex"; checked: root.settings ? root.settings.showCodex : false; onToggled: { root.settings.showCodex = checked; root.save() } }
                SpinBox { from: 50; to: 600; value: root.settings ? root.settings.codexWidth : 170; editable: true; onValueModified: { root.settings.codexWidth = value; root.save() } }
                Label { text: i18n("Width") }

                CheckBox { text: "Toggl Track"; checked: root.settings ? root.settings.showToggl : false; onToggled: { root.settings.showToggl = checked; root.save() } }
                SpinBox { from: 50; to: 600; value: root.settings ? root.settings.togglWidth : 170; editable: true; onValueModified: { root.settings.togglWidth = value; root.save() } }
                Label { text: i18n("Width") }

                CheckBox { text: "JIRA"; checked: root.settings ? root.settings.showJira : false; onToggled: { root.settings.showJira = checked; root.save() } }
                SpinBox { from: 50; to: 600; value: root.settings ? root.settings.jiraWidth : 170; editable: true; onValueModified: { root.settings.jiraWidth = value; root.save() } }
                Label { text: i18n("Width") }
            }
        }

        GroupBox {
            title: i18n("Behavior")
            Layout.fillWidth: true
            ColumnLayout {
                anchors.fill: parent
                CheckBox {
                    text: i18n("Threshold notifications (75%, 90%)")
                    checked: root.settings ? root.settings.notificationsEnabled : false
                    onToggled: { root.settings.notificationsEnabled = checked; root.save() }
                }
                CheckBox {
                    text: i18n("Notify on limit reset")
                    checked: root.settings ? root.settings.notifyOnReset : false
                    onToggled: { root.settings.notifyOnReset = checked; root.save() }
                }
                CheckBox {
                    text: i18n("Incognito mode")
                    checked: root.settings ? root.settings.incognitoMode : false
                    onToggled: { root.settings.incognitoMode = checked; root.save() }
                }
            }
        }
    }
}
