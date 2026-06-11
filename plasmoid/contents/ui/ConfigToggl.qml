import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "../code/api.js" as Api

ConfigPageBase {
    id: root
    property var projects: []
    property string statusText: ""

    onLoaded: if (root.settings && root.settings.togglApiKeyConfigured) loadProjects()

    ColumnLayout {
        width: root.availableWidth
        spacing: 12

        RowLayout {
            Layout.fillWidth: true
            Label { text: "Toggl Track"; font.bold: true; Layout.fillWidth: true }
            Button { text: i18n("Refresh"); onClicked: Api.refresh("toggl", function() { root.reload() }) }
        }

        GridLayout {
            columns: 2
            Layout.fillWidth: true
            Label { text: i18n("API key") }
            TextField {
                Layout.fillWidth: true
                echoMode: TextInput.Password
                text: ""
                placeholderText: root.settings && root.settings.togglApiKeyConfigured ? i18n("Configured") : ""
                onEditingFinished: {
                    root.settings.togglApiKey = text.trim()
                    saveAndRefresh(loadProjects)
                }
            }

            Label { text: i18n("Monthly target (Kč)") }
            TextField {
                Layout.fillWidth: true
                echoMode: root.settings && root.settings.incognitoMode ? TextInput.Password : TextInput.Normal
                horizontalAlignment: TextInput.AlignRight
                text: root.settings && root.settings.togglMonthlyTargetCzk > 0 ? root.settings.togglMonthlyTargetCzk : ""
                onEditingFinished: {
                    root.settings.togglMonthlyTargetCzk = parseFloat(text) || 0
                    saveAndRefresh()
                }
            }

            Label { text: i18n("Workday hours (start-end)") }
            RowLayout {
                TextField {
                    Layout.preferredWidth: 70
                    horizontalAlignment: TextInput.AlignRight
                    text: root.settings ? root.settings.workdayStartHour : "9"
                    onEditingFinished: {
                        root.settings.workdayStartHour = parseFloat(text) || 9
                        saveAndRefresh()
                    }
                }
                Label { text: "-" }
                TextField {
                    Layout.preferredWidth: 70
                    horizontalAlignment: TextInput.AlignRight
                    text: root.settings ? root.settings.workdayEndHour : "17"
                    onEditingFinished: {
                        root.settings.workdayEndHour = parseFloat(text) || 17
                        saveAndRefresh()
                    }
                }
            }
        }

        Label { text: statusText; color: statusText.indexOf("✗") === 0 ? "#F44336" : "#4CAF50"; visible: statusText.length > 0 }

        GroupBox {
            title: i18n("Project rates (Kč/h)")
            Layout.fillWidth: true
            visible: projects.length > 0
            ColumnLayout {
                anchors.fill: parent
                Repeater {
                    model: projects.filter(function(p) { return p.active })
                    delegate: RowLayout {
                        Layout.fillWidth: true
                        Label { Layout.fillWidth: true; text: modelData.clientName ? modelData.clientName + " / " + modelData.name : modelData.name; elide: Text.ElideRight }
                        TextField {
                            Layout.preferredWidth: 80
                            horizontalAlignment: TextInput.AlignRight
                            echoMode: root.settings && root.settings.incognitoMode ? TextInput.Password : TextInput.Normal
                            text: root.settings && root.settings.togglProjectRates && root.settings.togglProjectRates[modelData.id] ? root.settings.togglProjectRates[modelData.id] : ""
                            onEditingFinished: {
                                if (!root.settings.togglProjectRates)
                                    root.settings.togglProjectRates = {}
                                var rate = parseFloat(text) || 0
                                if (rate > 0)
                                    root.settings.togglProjectRates[modelData.id] = rate
                                else
                                    delete root.settings.togglProjectRates[modelData.id]
                                saveAndRefresh()
                            }
                        }
                    }
                }
            }
        }
    }

    function loadProjects() {
        statusText = i18n("Validating…")
        Api.loadTogglProjects(function(data, error) {
            if (data) {
                projects = data
                statusText = i18n("✓ Connected — %1 project(s)", data.length)
            } else {
                projects = []
                statusText = "✗ " + error
            }
        })
    }

    function saveAndRefresh(afterSave) {
        root.save(function(data, error) {
            if (error) {
                statusText = "✗ " + error
                return
            }
            if (afterSave)
                afterSave()
            Api.refresh("toggl", function() {
                root.reload()
            })
        })
    }
}
