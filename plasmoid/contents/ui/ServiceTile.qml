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
    signal unhovered()
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
            width: Math.max(0, root.width - 28)
            height: root.height - 10
            anchors.verticalCenter: parent.verticalCenter
            spacing: root.bars.length <= 2 ? 5 : root.bars.length <= 4 ? 3 : 2

            Repeater {
                model: root.bars
                delegate: ProgressBarLite {
                    width: barColumn.width
                    height: (barColumn.height - Math.max(0, root.bars.length - 1) * barColumn.spacing) / Math.max(1, root.bars.length)
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
        onEntered: root.hovered(root)
        onExited: root.unhovered()
        onDoubleClicked: root.forceRefresh()
    }
}
