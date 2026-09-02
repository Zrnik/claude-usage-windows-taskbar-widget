import QtQuick
import org.kde.plasma.core as PlasmaCore

PlasmaCore.Dialog {
    id: root
    property alias content: contentColumn.data
    property int panelLocation: PlasmaCore.Types.BottomEdge
    property var pendingTile: null
    property var hoveredTile: null
    property bool popupHovered: false
    readonly property int gap: 6

    type: PlasmaCore.Dialog.PopupMenu
    flags: Qt.WindowStaysOnTopHint
    // Hover state below decides when to close. Hiding on deactivation races
    // with moving the pointer from the panel into this separate dialog window.
    hideOnWindowDeactivate: false
    location: panelLocation

    mainItem: Rectangle {
        width: 280
        height: contentColumn.implicitHeight + 20
        color: Style.popupBg
        radius: 6

        Timer {
            id: closeTimer
            interval: 200
            repeat: false
            onTriggered: {
                if (!root.hoveredTile && !root.popupHovered)
                    root.close()
            }
        }

        HoverHandler {
            onHoveredChanged: root.setPopupHovered(hovered)
        }

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
        closeTimer.stop()
        hoveredTile = tile
        pendingTile = tile
        visualParent = tile
        Qt.callLater(function() {
            if (pendingTile === tile && hoveredTile === tile)
                visible = true
        })
    }

    function requestClose(tile) {
        if (!tile || hoveredTile === tile)
            hoveredTile = null
        if (!popupHovered)
            closeTimer.restart()
    }

    function setPopupHovered(hovering) {
        popupHovered = hovering
        if (hovering)
            closeTimer.stop()
        else if (!hoveredTile)
            closeTimer.restart()
    }

    function close() {
        closeTimer.stop()
        visible = false
        pendingTile = null
        hoveredTile = null
        popupHovered = false
    }
}
