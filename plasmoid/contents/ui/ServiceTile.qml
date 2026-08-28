import QtQuick
import "Format.js" as Format

Item {
    id: root
    property string iconSource: ""
    property real tileWidth: 170
    property var bars: []
    property bool loading: false
    property string errorText: ""
    signal hovered(var tile)
    signal unhovered(var tile)
    signal forceRefresh()

    width: tileWidth
    height: parent ? parent.height : 48

    Row {
        anchors.fill: parent
        anchors.margins: 0

        Item {
            width: 24
            height: root.height
            Image {
                width: 20
                height: 20
                anchors.centerIn: parent
                source: root.iconSource
                fillMode: Image.PreserveAspectFit
                smooth: true
            }
        }

        Item { width: 4; height: root.height }

        Column {
            id: barColumn
            readonly property int barCount: Math.max(1, root.bars.length)
            readonly property int gap: root.bars.length <= 2 ? 3 : root.bars.length <= 4 ? 2 : 1
            readonly property int barHeight: Math.max(7, Math.floor((Math.max(8, root.height - 8) - Math.max(0, barCount - 1) * gap) / barCount))
            width: Math.max(0, root.width - 28)
            height: barCount * barHeight + Math.max(0, barCount - 1) * gap
            anchors.verticalCenter: parent.verticalCenter
            spacing: gap

            Repeater {
                model: root.bars
                delegate: ProgressBarLite {
                    width: barColumn.width
                    height: barColumn.barHeight
                    fontSize: Math.max(5, Math.min(8, Math.floor(barColumn.barHeight * 0.70)))
                    value: modelData.value
                    fillColor: root.errorText ? Style.maroon : modelData.color
                    leftText: modelData.leftText || ""
                    rightText: modelData.rightText || ""
                    centerText: modelData.centerText || ""
                }
            }
        }
    }

    MouseArea {
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton
        onEntered: root.hovered(root)
        onExited: root.unhovered(root)
        onDoubleClicked: root.forceRefresh()
    }
}
