import QtQuick
import org.kde.plasma.core as PlasmaCore

PlasmaCore.Dialog {
    id: root
    property alias content: contentColumn.data
    property int panelLocation: PlasmaCore.Types.BottomEdge
    property var pendingTile: null
    readonly property int gap: 6

    type: PlasmaCore.Dialog.PopupMenu
    flags: Qt.WindowStaysOnTopHint
    hideOnWindowDeactivate: true
    location: panelLocation

    mainItem: Rectangle {
        width: 280
        height: contentColumn.implicitHeight + 20
        color: Style.popupBg
        radius: 6

        Column {
            id: contentColumn
            anchors.fill: parent
            anchors.margins: 10
            spacing: 0
        }
    }

    function showFor(tile) {
        if (!tile)
            return
        pendingTile = tile
        visualParent = tile
        Qt.callLater(function() {
            if (pendingTile === tile)
                visible = true
        })
    }

    function close() {
        visible = false
        pendingTile = null
    }
}
