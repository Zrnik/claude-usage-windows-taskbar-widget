import QtQuick
import "Format.js" as Format

Column {
    id: root
    property var account: null
    property var state: null
    width: 260
    spacing: 0

    Repeater {
        model: visibleLimits()
        delegate: Column {
            width: root.width
            spacing: 0

            Text {
                width: parent.width
                text: Format.labelSuffix(modelData.label) + spendSuffix()
                color: Style.muted
                font.pixelSize: 9
                height: 13
                elide: Text.ElideRight
            }

            ProgressBarLite {
                width: parent.width
                height: 12
                radius: 2
                value: modelData.utilization
                fillColor: Format.barColor(modelData.utilization)
                centerText: Math.round(modelData.utilization) + "%"
            }

            Row {
                width: parent.width
                height: 14
                Text {
                    width: parent.width * 0.55
                    text: "Reset: " + Format.resetTime(modelData.resetsAt)
                    color: "#D3D3D3"
                    font.pixelSize: 9
                    elide: Text.ElideRight
                }
                Text {
                    width: parent.width * 0.45
                    text: Format.localDateTime(modelData.resetsAt)
                    color: Style.muted
                    font.pixelSize: 9
                    horizontalAlignment: Text.AlignRight
                    elide: Text.ElideRight
                }
            }

            Text {
                width: parent.width
                text: "History (" + Format.historyWindowLabel(modelData.label, root.state && root.state.settings ? root.state.settings.chartWindowHours : {}) + ")"
                color: Style.dim
                font.pixelSize: 8
                height: 14
            }

            HistoryChart {
                width: parent.width
                records: modelData.history || []
                label: modelData.label
                overrides: root.state && root.state.settings ? root.state.settings.chartWindowHours : {}
            }

            Text {
                width: parent.width
                text: prediction(modelData)
                visible: text.length > 0
                color: predictionColor(modelData)
                font.pixelSize: 9
                height: visible ? 14 : 0
                elide: Text.ElideRight
            }

            Item { width: 1; height: 6 }

            function spendSuffix() {
                if (modelData.label === "spend" && root.account && root.account.usage &&
                    root.account.usage.spendUsed !== undefined && root.account.usage.spendLimit !== undefined)
                    return "  $" + Number(root.account.usage.spendUsed).toFixed(2) + " / $" + Number(root.account.usage.spendLimit).toFixed(2)
                return ""
            }
        }
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: root.account && root.account.lastError; }
    Item { width: 1; height: 4; visible: root.account && root.account.lastError }
    Text {
        width: parent.width
        text: root.account && root.account.lastError ? root.account.lastError : ""
        color: Style.red
        font.pixelSize: 9
        wrapMode: Text.Wrap
        visible: text.length > 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; }
    Item { width: 1; height: 4 }

    Row {
        width: parent.width
        Text {
            width: parent.width - 50
            text: root.account ? root.account.credentialPath : ""
            color: Style.dim
            font.pixelSize: 8
            wrapMode: Text.Wrap
            elide: Text.ElideRight
        }
        Text {
            width: 50
            text: "v" + (root.state ? root.state.version : "")
            color: Style.version
            font.pixelSize: 8
            horizontalAlignment: Text.AlignRight
        }
    }

    function visibleLimits() {
        if (!account || !account.usage || !account.usage.limits)
            return []
        var hidden = state && state.settings && state.settings.hiddenLimits ? state.settings.hiddenLimits : []
        var out = []
        for (var i = 0; i < account.usage.limits.length; i++) {
            if (hidden.indexOf(account.usage.limits[i].label) >= 0)
                continue
            var item = account.usage.limits[i]
            item.history = account.history || []
            out.push(item)
        }
        return out
    }

    function prediction(limit) {
        if (limit.utilization >= 100)
            return "Limit reached"
        return ""
    }

    function predictionColor(limit) {
        if (limit.utilization >= 100)
            return Style.red
        if (limit.utilization >= 90)
            return Style.orange
        return Style.muted
    }
}
