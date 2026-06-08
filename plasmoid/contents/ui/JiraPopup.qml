import QtQuick
import "Format.js" as Format

Column {
    id: root
    property var service: null
    property var daemonState: null
    width: 260
    spacing: 0

    Text { text: "JIRA · " + (usage() ? usage().projectKey : ""); color: Style.muted; font.pixelSize: 9; height: 16 }

    Text { text: myTotal() > 0 ? "MY ISSUES" : "No issues assigned to you in this project"; color: myTotal() > 0 ? Style.muted : Style.muted; font.pixelSize: 9; height: 16 }

    Repeater {
        model: myTotal() > 0 ? [
            { label: "To Do", value: cat("new"), color: "#999999" },
            { label: "In Progress", value: cat("indeterminate"), color: Style.orange },
            { label: "Done", value: cat("done"), color: Style.green }
        ] : []
        delegate: Row {
            width: root.width
            height: 14
            Text { width: parent.width - 30; text: modelData.label; color: modelData.color; font.pixelSize: 9 }
            Text { width: 30; text: modelData.value; color: Style.text; font.pixelSize: 9; font.bold: true; horizontalAlignment: Text.AlignRight }
        }
    }

    Text {
        width: parent.width
        text: usage() && myTotal() > 0 ? "Story points: " + Number(usage().myDoneStoryPoints || 0).toFixed(1).replace(".0", "") + " done / " + Number(usage().myStoryPoints || 0).toFixed(1).replace(".0", "") + " total" : ""
        color: Style.subtext
        font.pixelSize: 9
        height: text.length > 0 ? 18 : 0
        visible: text.length > 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: active().length > 0 }
    Item { width: 1; height: 4; visible: active().length > 0 }
    Text { text: "ACTIVE TASKS (" + active().length + ")"; color: Style.muted; font.pixelSize: 9; height: 14; visible: active().length > 0 }
    Repeater {
        model: active().slice(0, 10)
        delegate: Row {
            width: root.width
            height: 15
            Text {
                width: 50
                text: modelData.key
                color: statusColor(modelData.statusCategory)
                font.pixelSize: 9
                font.bold: true
                elide: Text.ElideRight
            }
            Text {
                width: 110
                text: modelData.summary
                color: Style.subtext
                font.pixelSize: 9
                elide: Text.ElideRight
            }
            Text {
                width: root.width - 160
                text: modelData.storyPoints > 0 ? modelData.statusName + " · " + Number(modelData.storyPoints).toFixed(1).replace(".0", "") + " SP" : modelData.statusName
                color: statusColor(modelData.statusCategory)
                font.pixelSize: 8
                horizontalAlignment: Text.AlignRight
                elide: Text.ElideRight
            }
        }
    }
    Text {
        text: active().length > 10 ? "+ " + (active().length - 10) + " more" : ""
        color: Style.dim
        font.pixelSize: 8
        font.italic: true
        visible: text.length > 0
        height: visible ? 13 : 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: history().length >= 2 }
    Item { width: 1; height: 4; visible: history().length >= 2 }
    Text { text: "TRENDS"; color: Style.muted; font.pixelSize: 9; height: 14; visible: history().length >= 2 }
    Text { width: parent.width; text: velocityText(); color: Style.subtext; font.pixelSize: 9; height: history().length >= 2 ? 14 : 0; visible: history().length >= 2 }
    JiraCharts { width: parent.width; data: usage(); history: history(); visible: history().length >= 2 }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: ranking().length > 1 }
    Item { width: 1; height: 4; visible: ranking().length > 1 }
    Text { text: "RANKING (by SP done)"; color: Style.muted; font.pixelSize: 9; height: 14; visible: ranking().length > 1 }
    Repeater {
        model: ranking()
        delegate: Row {
            width: root.width
            height: 15
            property bool isMe: usage() && usage().me && modelData.accountId === usage().me.accountId
            Text { width: 24; text: "#" + (index + 1); color: isMe ? Style.gold : Style.muted; font.pixelSize: 9; font.bold: isMe }
            Text { width: 146; text: isMe ? modelData.displayName + " (you)" : modelData.displayName; color: isMe ? Style.text : Style.subtext; font.pixelSize: 9; font.bold: isMe; elide: Text.ElideRight }
            Text { width: root.width - 170; text: Number(modelData.doneStoryPoints || 0).toFixed(1).replace(".0", "") + " SP · " + modelData.doneIssues + "/" + modelData.totalIssues; color: Style.subtext; font.pixelSize: 9; horizontalAlignment: Text.AlignRight }
        }
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: service && service.lastError }
    Text {
        width: parent.width
        text: service && service.lastError ? service.lastError : ""
        color: Style.red
        font.pixelSize: 9
        wrapMode: Text.Wrap
        visible: text.length > 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; }
    Item { width: 1; height: 4 }
    Row {
        width: parent.width
        Text { width: parent.width - 50; text: "JIRA"; color: Style.dim; font.pixelSize: 8 }
        Text { width: 50; text: "v" + (daemonState ? daemonState.version : ""); color: Style.version; font.pixelSize: 8; horizontalAlignment: Text.AlignRight }
    }

    function usage() { return service ? service.usage : null }
    function cat(name) { return usage() && usage().myByCategory && usage().myByCategory[name] ? usage().myByCategory[name] : 0 }
    function myTotal() { return cat("new") + cat("indeterminate") + cat("done") }
    function active() { return usage() && usage().myActiveIssues ? usage().myActiveIssues : [] }
    function ranking() { return usage() && usage().developerRanking ? usage().developerRanking : [] }
    function history() { return service && service.history ? service.history : [] }
    function statusColor(cat) { return cat === "indeterminate" ? Style.orange : cat === "new" ? Style.muted : Style.subtext }
    function velocityText() {
        var h = history()
        if (h.length < 2) return ""
        var last7 = 0
        var last28 = 0
        var now = Date.now()
        for (var i = 1; i < h.length; i++) {
            var delta = Math.max(0, (h[i].myDoneIssues || 0) - (h[i - 1].myDoneIssues || 0))
            var ts = new Date(h[i].date).getTime()
            if (now - ts <= 7 * 86400000) last7 += delta
            if (now - ts <= 28 * 86400000) last28 += delta
        }
        return "Velocity: " + last7.toFixed(1).replace(".0", "") + "/wk (last 7d) · " + (last28 / 4).toFixed(1).replace(".0", "") + "/wk (4w avg)"
    }
}
