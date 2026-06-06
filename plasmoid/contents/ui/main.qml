import QtQuick
import QtQuick.Layouts
import QtQuick.Controls
import org.kde.plasma.plasmoid
import "../code/api.js" as Api
import "Format.js" as Format

PlasmoidItem {
    id: root
    property var state: null
    property var activeTile: null
    property string activeKind: ""
    property int activeIndex: -1

    preferredRepresentation: compactRepresentation
    compactRepresentation: Item {
        id: compactRoot
        implicitWidth: row.implicitWidth
        implicitHeight: 48

        Row {
            id: row
            anchors.fill: parent
            spacing: 0

            Repeater {
                model: accountTiles()
                delegate: ServiceTile {
                    tileWidth: modelData.width
                    iconSource: modelData.service === "codex" ? "../images/codex-logo.png" : "../images/claude-logo.png"
                    bars: modelData.bars
                    errorText: modelData.errorText
                    onHovered: {
                        root.activeTile = tile
                        root.activeKind = "account"
                        root.activeIndex = modelData.index
                        popupLoader.sourceComponent = accountPopupComponent
                        popup.showFor(tile)
                    }
                    onUnhovered: popup.close()
                    onForceRefresh: Api.refresh(modelData.service, pollState)
                }
            }

            TogglTile {
                visible: showToggl()
                tileWidth: root.state && root.state.settings ? root.state.settings.togglWidth : 170
                data: root.state && root.state.toggl ? root.state.toggl.usage : null
                incognito: root.state && root.state.settings ? root.state.settings.incognitoMode : false
                errorText: root.state && root.state.toggl ? root.state.toggl.lastError || "" : ""
                onHovered: {
                    root.activeTile = tile
                    root.activeKind = "toggl"
                    popupLoader.sourceComponent = togglPopupComponent
                    popup.showFor(tile)
                }
                onUnhovered: popup.close()
                onForceRefresh: Api.refresh("toggl", pollState)
            }

            JiraTile {
                visible: showJira()
                tileWidth: root.state && root.state.settings ? root.state.settings.jiraWidth : 170
                data: root.state && root.state.jira ? root.state.jira.usage : null
                errorText: root.state && root.state.jira ? root.state.jira.lastError || "" : ""
                onHovered: {
                    root.activeTile = tile
                    root.activeKind = "jira"
                    popupLoader.sourceComponent = jiraPopupComponent
                    popup.showFor(tile)
                }
                onUnhovered: popup.close()
                onForceRefresh: Api.refresh("jira", pollState)
            }
        }

        PopupShell {
            id: popup
            parent: compactRoot
            content: [
                Loader {
                    id: popupLoader
                    width: 260
                }
            ]
        }
    }

    Timer {
        interval: 15000
        running: true
        repeat: true
        onTriggered: pollState()
    }

    Timer {
        interval: 1000
        running: true
        repeat: true
        onTriggered: if (!root.state) pollState()
    }

    Component.onCompleted: pollState()

    Component {
        id: accountPopupComponent
        ClaudeCodexPopup {
            account: root.state && root.activeIndex >= 0 ? root.state.accounts[root.activeIndex] : null
            state: root.state
        }
    }

    Component {
        id: togglPopupComponent
        TogglPopup {
            service: root.state ? root.state.toggl : null
            state: root.state
        }
    }

    Component {
        id: jiraPopupComponent
        JiraPopup {
            service: root.state ? root.state.jira : null
            state: root.state
        }
    }

    function pollState() {
        Api.loadState(function(data, error) {
            if (data)
                root.state = data
        })
    }

    function accountTiles() {
        if (!root.state || !root.state.accounts || !root.state.settings)
            return []
        var out = []
        for (var i = 0; i < root.state.accounts.length; i++) {
            var account = root.state.accounts[i]
            if (account.service === "claude" && !root.state.settings.showClaude)
                continue
            if (account.service === "codex" && !root.state.settings.showCodex)
                continue
            var hidden = root.state.settings.hiddenLimits || []
            var visibleLimits = account.usage && account.usage.limits ? account.usage.limits.filter(function(l) {
                return hidden.indexOf(l.label) < 0
            }) : []
            var bars = []
            var showText = visibleLimits.length <= 4
            if (visibleLimits.length === 0) {
                bars = [{ value: account.lastError ? 100 : 0, color: account.lastError ? Style.maroon : Style.green, centerText: account.lastError ? "Error" : "|" }]
            } else {
                for (var j = 0; j < visibleLimits.length; j++) {
                    bars.push({
                        value: visibleLimits[j].utilization,
                        color: Format.barColor(visibleLimits[j].utilization),
                        leftText: showText ? Math.round(visibleLimits[j].utilization) + "%" : "",
                        rightText: showText ? Format.resetTime(visibleLimits[j].resetsAt) : ""
                    })
                }
            }
            out.push({
                index: i,
                service: account.service,
                width: account.service === "codex" ? root.state.settings.codexWidth : root.state.settings.claudeWidth,
                bars: bars,
                errorText: account.lastError || ""
            })
        }
        return out
    }

    function showToggl() {
        return root.state && root.state.settings && root.state.settings.showToggl &&
            (root.state.settings.togglApiKey || (root.state.toggl && root.state.toggl.usage))
    }

    function showJira() {
        if (!root.state || !root.state.settings || !root.state.settings.showJira)
            return false
        var s = root.state.settings
        return (s.jiraUrl && s.jiraEmail && s.jiraApiToken && s.jiraProjectKey) ||
            (root.state.jira && root.state.jira.usage)
    }
}
