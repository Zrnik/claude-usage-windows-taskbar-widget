import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "../code/api.js" as Api

ConfigPageBase {
    id: root
    property var projects: []
    property var users: []
    property string statusText: ""

    onLoaded: if (root.settings && root.settings.jiraUrl && root.settings.jiraEmail && root.settings.jiraApiToken) loadProjects()

    ColumnLayout {
        width: root.availableWidth
        spacing: 12

        RowLayout {
            Layout.fillWidth: true
            Label { text: "JIRA"; font.bold: true; Layout.fillWidth: true }
            Button { text: i18n("Refresh"); onClicked: Api.refresh("jira", function() { root.reload() }) }
        }

        GridLayout {
            columns: 2
            Layout.fillWidth: true
            Label { text: i18n("Site URL") }
            TextField { Layout.fillWidth: true; text: root.settings ? root.settings.jiraUrl : ""; placeholderText: "https://yourcompany.atlassian.net"; onEditingFinished: { root.settings.jiraUrl = text.trim(); root.save(); loadProjects() } }
            Label { text: i18n("Email") }
            TextField { Layout.fillWidth: true; text: root.settings ? root.settings.jiraEmail : ""; onEditingFinished: { root.settings.jiraEmail = text.trim(); root.save(); loadProjects() } }
            Label { text: i18n("API token") }
            TextField { Layout.fillWidth: true; echoMode: TextInput.Password; text: root.settings ? root.settings.jiraApiToken : ""; onEditingFinished: { root.settings.jiraApiToken = text.trim(); root.save(); loadProjects() } }
            Label { text: i18n("Project") }
            ComboBox {
                Layout.fillWidth: true
                model: projects
                textRole: "display"
                onActivated: {
                    if (currentIndex >= 0 && projects[currentIndex]) {
                        root.settings.jiraProjectKey = projects[currentIndex].key
                        root.save()
                        loadUsers()
                    }
                }
            }
        }

        Label { text: statusText; color: statusText.indexOf("✗") === 0 ? "#F44336" : "#4CAF50"; visible: statusText.length > 0 }

        GroupBox {
            title: i18n("Compare with developers")
            Layout.fillWidth: true
            visible: users.length > 0
            ColumnLayout {
                anchors.fill: parent
                Repeater {
                    model: users
                    delegate: CheckBox {
                        Layout.fillWidth: true
                        text: modelData.displayName + (modelData.emailAddress ? "  (" + modelData.emailAddress + ")" : "")
                        checked: root.settings && root.settings.jiraDeveloperAccountIds ? root.settings.jiraDeveloperAccountIds.indexOf(modelData.accountId) >= 0 : false
                        onToggled: {
                            if (!root.settings.jiraDeveloperAccountIds)
                                root.settings.jiraDeveloperAccountIds = []
                            var idx = root.settings.jiraDeveloperAccountIds.indexOf(modelData.accountId)
                            if (checked && idx < 0)
                                root.settings.jiraDeveloperAccountIds.push(modelData.accountId)
                            if (!checked && idx >= 0)
                                root.settings.jiraDeveloperAccountIds.splice(idx, 1)
                            root.save()
                        }
                    }
                }
            }
        }
    }

    function loadProjects() {
        if (!root.settings || !root.settings.jiraUrl || !root.settings.jiraEmail || !root.settings.jiraApiToken)
            return
        statusText = i18n("Validating…")
        Api.loadJiraProjects(function(data, error) {
            if (data) {
                projects = data
                statusText = root.settings.jiraProjectKey
                    ? i18n("✓ Connected — loading users…")
                    : i18n("✓ Connected — pick a project (%1 available)", data.length)
                loadUsers()
            } else {
                projects = []
                users = []
                statusText = "✗ " + error
            }
        })
    }

    function loadUsers() {
        if (!root.settings || !root.settings.jiraProjectKey)
            return
        Api.loadJiraUsers(function(data, error) {
            if (data) {
                users = data
                statusText = i18n("✓ Connected — %1 user(s) in %2", data.length, root.settings.jiraProjectKey)
            } else {
                users = []
                statusText = "✗ " + error
            }
        })
    }
}
