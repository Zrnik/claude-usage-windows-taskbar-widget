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
    readonly property bool togglVisible: root.daemonState && root.daemonState.settings && root.daemonState.settings.showToggl === true
    readonly property bool jiraVisible: showJira()
    readonly property int panelWidth: compactWidth()

    width: panelWidth
    implicitWidth: panelWidth
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

            ServiceTile {
                visible: root.togglVisible
                tileWidth: root.daemonState && root.daemonState.settings ? root.daemonState.settings.togglWidth : 170
                iconSource: "../images/toggl-logo.png"
                bars: togglBars()
                errorText: root.daemonState && root.daemonState.toggl && !root.daemonState.toggl.usage ? root.daemonState.toggl.lastError || "" : ""
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
                visible: root.jiraVisible
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
        if (out.length === 0 && !root.togglVisible && !root.jiraVisible) {
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

    function togglBars() {
        var service = root.daemonState ? root.daemonState.toggl : null
        var usage = service ? service.usage : null
        if (!usage) {
            var label = service && service.lastError
                ? (service.lastError.toLowerCase().indexOf("rate limit") >= 0 ? "Rate limit" : "Toggl error")
                : "Toggl"
            return [{ value: service && service.lastError ? 100 : 0, color: service && service.lastError ? Style.maroon : Style.green, centerText: label }]
        }

        var earned = Number(usage.earnedCzk || 0)
        var target = Number(usage.targetCzk || 0)
        var monthlyPct = target > 0 ? Math.min(100, earned / target * 100) : 0
        var dailyTarget = togglRequiredHoursPerDay(usage)
        var today = togglTodayHours(usage)
        var todayPct = dailyTarget > 0 ? Math.min(100, today / dailyTarget * 100) : 0
        var incognito = root.daemonState && root.daemonState.settings && root.daemonState.settings.incognitoMode === true
        return [
            {
                value: monthlyPct,
                color: target > 0 && earned >= target ? Style.blue : Style.green,
                leftText: Math.round(monthlyPct) + "%",
                rightText: target > 0 ? Format.shortCzk(earned, incognito) + "/" + Format.shortCzk(target, incognito) : Format.shortCzk(earned, incognito)
            },
            {
                value: todayPct,
                color: today >= dailyTarget ? Style.blue : today >= dailyTarget * 0.5 ? Style.green : Style.orange,
                leftText: today.toFixed(1).replace(".0", "") + "h",
                rightText: "need " + dailyTarget.toFixed(1).replace(".0", "") + "h"
            }
        ]
    }

    function togglTodayHours(usage) {
        if (!usage || !usage.dailyBreakdown)
            return 0
        var today = new Date().toDateString()
        for (var i = 0; i < usage.dailyBreakdown.length; i++) {
            if (new Date(usage.dailyBreakdown[i].date).toDateString() === today)
                return Number(usage.dailyBreakdown[i].hours || 0)
        }
        return 0
    }

    function togglRequiredHoursPerDay(usage) {
        if (!usage || Number(usage.targetCzk || 0) <= 0)
            return 8
        var remaining = Math.max(0, Number(usage.targetCzk || 0) - Number(usage.earnedCzk || 0))
        var monthStart = new Date(usage.monthStart)
        var monthEnd = new Date(usage.monthResetsAt)
        var workdays = 0
        for (var d = new Date(monthStart); d < monthEnd; d.setDate(d.getDate() + 1)) {
            if (d.getDay() !== 0 && d.getDay() !== 6)
                workdays++
        }
        var impliedRate = workdays > 0 ? Number(usage.targetCzk || 0) / (workdays * 8.0) : 0
        var remainingDays = 0
        for (var r = new Date(); r < monthEnd; r.setDate(r.getDate() + 1)) {
            if (r.getDay() !== 0 && r.getDay() !== 6)
                remainingDays++
        }
        return impliedRate > 0 && remainingDays > 0 ? remaining / (impliedRate * remainingDays) : 8
    }

    function compactWidth() {
        if (!root.daemonState || !root.daemonState.settings)
            return 170
        var total = 0
        var tiles = accountTiles()
        for (var i = 0; i < tiles.length; i++)
            total += Number(tiles[i].width || 0)
        if (root.togglVisible)
            total += Number(root.daemonState.settings.togglWidth || 170)
        if (root.jiraVisible)
            total += Number(root.daemonState.settings.jiraWidth || 170)
        return Math.max(1, total)
    }

    function showToggl() {
        return root.togglVisible
    }

    function showJira() {
        if (!root.daemonState || !root.daemonState.settings || !root.daemonState.settings.showJira)
            return false
        var s = root.daemonState.settings
        return (s.jiraUrl && s.jiraEmail && s.jiraApiTokenConfigured && s.jiraProjectKey) ||
            (root.daemonState.jira && root.daemonState.jira.usage)
    }
}
