import QtQuick
import QtQuick.Layouts
import QtQuick.Controls
import org.kde.plasma.core as PlasmaCore
import org.kde.plasma.plasmoid
import "../code/api.js" as Api
import "Format.js" as Format

PlasmoidItem {
    id: root
    property var daemonState: null
    property var activeTile: null
    property string activeKind: ""
    property int activeIndex: -1
    readonly property int fallbackPanelHeight: 36
    readonly property int panelWidth: compactWidth()

    width: panelWidth
    implicitHeight: fallbackPanelHeight
    Layout.minimumWidth: panelWidth
    Layout.preferredWidth: panelWidth
    Layout.maximumWidth: panelWidth
    Layout.minimumHeight: 24
    Layout.preferredHeight: fallbackPanelHeight
    Layout.fillHeight: true
    Plasmoid.status: panelWidth > 1 ? PlasmaCore.Types.ActiveStatus : PlasmaCore.Types.PassiveStatus
    Plasmoid.contextualActions: [
        PlasmaCore.Action {
            text: i18n("Incognito mode")
            icon.name: "view-private"
            checkable: true
            checked: root.daemonState && root.daemonState.settings ? root.daemonState.settings.incognitoMode : false
            onTriggered: toggleIncognito()
        },
        PlasmaCore.Action {
            text: i18n("Refresh Toggl")
            icon.name: "view-refresh"
            enabled: showToggl()
            onTriggered: Api.refresh("toggl", pollState)
        },
        PlasmaCore.Action {
            text: i18n("Aktualizovat")
            icon.name: "system-software-update"
            onTriggered: Api.update(function(data, error) { pollState() })
        }
    ]
    preferredRepresentation: fullRepresentation
    fullRepresentation: Item {
        id: compactRoot
        width: root.panelWidth
        height: root.height > 0 ? root.height : root.fallbackPanelHeight
        implicitWidth: root.panelWidth
        implicitHeight: root.fallbackPanelHeight
        Layout.minimumWidth: root.panelWidth
        Layout.preferredWidth: root.panelWidth
        Layout.maximumWidth: root.panelWidth
        Layout.minimumHeight: 24
        Layout.preferredHeight: root.fallbackPanelHeight
        Layout.fillHeight: true

        Row {
            id: row
            width: root.panelWidth
            height: compactRoot.height
            spacing: 0

            Repeater {
                model: accountTiles()
                delegate: ServiceTile {
                    tileWidth: modelData.width
                    iconSource: modelData.service === "codex" ? "../images/codex-logo.png" : "../images/claude-logo.png"
                    bars: modelData.bars
                    errorText: modelData.errorText
                    onHovered: function(tile) {
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
                tileWidth: root.daemonState && root.daemonState.settings ? root.daemonState.settings.togglWidth : 170
                data: root.daemonState && root.daemonState.toggl ? root.daemonState.toggl.usage : null
                incognito: root.daemonState && root.daemonState.settings ? root.daemonState.settings.incognitoMode : false
                errorText: root.daemonState && root.daemonState.toggl ? root.daemonState.toggl.lastError || "" : ""
                onHovered: function(tile) {
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
                tileWidth: root.daemonState && root.daemonState.settings ? root.daemonState.settings.jiraWidth : 170
                data: root.daemonState && root.daemonState.jira ? root.daemonState.jira.usage : null
                errorText: root.daemonState && root.daemonState.jira ? root.daemonState.jira.lastError || "" : ""
                onHovered: function(tile) {
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
            panelLocation: Plasmoid.location
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
        onTriggered: if (!root.daemonState) pollState()
    }

    Component.onCompleted: pollState()

    Component {
        id: accountPopupComponent
        ClaudeCodexPopup {
            account: root.daemonState && root.activeIndex >= 0 ? root.daemonState.accounts[root.activeIndex] : null
            daemonState: root.daemonState
        }
    }

    Component {
        id: togglPopupComponent
        TogglPopup {
            service: root.daemonState ? root.daemonState.toggl : null
            daemonState: root.daemonState
        }
    }

    Component {
        id: jiraPopupComponent
        JiraPopup {
            service: root.daemonState ? root.daemonState.jira : null
            daemonState: root.daemonState
        }
    }

    function pollState() {
        Api.loadState(function(data, error) {
            if (data)
                root.daemonState = data
        })
    }

    function toggleIncognito() {
        if (!root.daemonState || !root.daemonState.settings)
            return
        var settings = root.daemonState.settings
        settings.incognitoMode = !settings.incognitoMode
        Api.saveSettings(settings, function(data, error) {
            if (data)
                root.daemonState.settings = data
            pollState()
        })
    }

    function accountTiles() {
        if (!root.daemonState || !root.daemonState.accounts || !root.daemonState.settings)
            return [{
                index: -1,
                service: "claude",
                width: 170,
                bars: [{ value: 0, color: Style.green, centerText: "|" }],
                errorText: ""
            }]
        var out = []
        for (var i = 0; i < root.daemonState.accounts.length; i++) {
            var account = root.daemonState.accounts[i]
            if (account.service === "claude" && !root.daemonState.settings.showClaude)
                continue
            if (account.service === "codex" && !root.daemonState.settings.showCodex)
                continue
            var hidden = root.daemonState.settings.hiddenLimits || []
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
                width: account.service === "codex" ? root.daemonState.settings.codexWidth : root.daemonState.settings.claudeWidth,
                bars: bars,
                errorText: account.lastError || ""
            })
        }
        if (out.length === 0 && !showToggl() && !showJira()) {
            out.push({
                index: -1,
                service: "claude",
                width: 170,
                bars: [{ value: 0, color: Style.green, centerText: "Setup" }],
                errorText: ""
            })
        }
        return out
    }

    function compactWidth() {
        if (!root.daemonState || !root.daemonState.settings)
            return 170
        var total = 0
        var tiles = accountTiles()
        for (var i = 0; i < tiles.length; i++)
            total += Number(tiles[i].width || 0)
        if (showToggl())
            total += Number(root.daemonState.settings.togglWidth || 170)
        if (showJira())
            total += Number(root.daemonState.settings.jiraWidth || 170)
        return Math.max(1, total)
    }

    function showToggl() {
        return root.daemonState && root.daemonState.settings && root.daemonState.settings.showToggl
    }

    function showJira() {
        if (!root.daemonState || !root.daemonState.settings || !root.daemonState.settings.showJira)
            return false
        var s = root.daemonState.settings
        return (s.jiraUrl && s.jiraEmail && s.jiraApiTokenConfigured && s.jiraProjectKey) ||
            (root.daemonState.jira && root.daemonState.jira.usage)
    }
}
