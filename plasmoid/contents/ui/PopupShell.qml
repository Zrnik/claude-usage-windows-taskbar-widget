import QtQuick
import QtQuick.Controls
import org.kde.plasma.core as PlasmaCore

Popup {
    id: root
    property alias content: contentColumn.data
    property int panelLocation: PlasmaCore.Types.BottomEdge
    property var pendingTile: null
    readonly property int gap: 6
    width: 280
    implicitHeight: contentColumn.implicitHeight
    height: implicitHeight
    padding: 0
    modal: false
    focus: false
    closePolicy: Popup.CloseOnEscape

    background: Rectangle {
        color: Style.popupBg
        radius: 6
    }

    contentItem: Column {
        id: contentColumn
        padding: 10
        spacing: 0
    }

    function showFor(tile) {
        if (!tile)
            return
        pendingTile = tile
        Qt.callLater(positionAndOpen)
    }

    function positionAndOpen() {
        var tile = pendingTile
        if (!tile || !root.parent)
            return

        var p = tile.mapToItem(root.parent, 0, 0)
        var popupHeight = Math.max(1, root.implicitHeight)

        if (panelLocation === PlasmaCore.Types.TopEdge) {
            x = p.x
            y = p.y + tile.height + gap
        } else if (panelLocation === PlasmaCore.Types.LeftEdge) {
            x = p.x + tile.width + gap
            y = p.y
        } else if (panelLocation === PlasmaCore.Types.RightEdge) {
            x = p.x - width - gap
            y = p.y
        } else {
            x = p.x
            y = p.y - popupHeight - gap
        }
        open()
    }
}
