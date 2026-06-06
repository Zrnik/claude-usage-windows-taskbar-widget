import QtQuick

Item {
    id: root
    property real value: 0
    property color fillColor: Style.green
    property string leftText: ""
    property string centerText: ""
    property string rightText: ""
    property int radius: 3
    property int fontSize: 9

    height: 12

    Rectangle {
        anchors.fill: parent
        color: Style.track
        radius: root.radius
    }

    Rectangle {
        anchors.left: parent.left
        anchors.top: parent.top
        anchors.bottom: parent.bottom
        width: Math.max(0, Math.min(parent.width, parent.width * Math.min(root.value, 100) / 100))
        color: root.fillColor
        radius: root.radius
    }

    Text {
        anchors.centerIn: parent
        width: parent.width
        text: root.centerText
        color: Style.text
        font.pixelSize: root.fontSize
        horizontalAlignment: Text.AlignHCenter
        verticalAlignment: Text.AlignVCenter
        elide: Text.ElideRight
    }

    Grid {
        anchors.fill: parent
        columns: 3
        visible: root.leftText !== "" || root.rightText !== ""

        Text {
            width: root.width * 0.35
            height: root.height
            text: root.leftText
            color: Style.text
            font.pixelSize: root.fontSize
            horizontalAlignment: Text.AlignRight
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
            minimumPixelSize: 6
            fontSizeMode: Text.Fit
        }
        Item { width: root.width * 0.20; height: root.height }
        Text {
            width: root.width * 0.45
            height: root.height
            text: root.rightText
            color: Style.text
            font.pixelSize: root.fontSize
            horizontalAlignment: Text.AlignLeft
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
            minimumPixelSize: 6
            fontSizeMode: Text.Fit
        }
    }
}
