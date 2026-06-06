import QtQuick
import QtQuick.Controls

Popup {
    id: root
    property alias content: contentColumn.data
    width: 280
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
        var p = tile.mapToItem(root.parent, 0, 0)
        x = p.x
        y = Math.max(0, p.y - implicitHeight - 4)
        open()
    }
}
