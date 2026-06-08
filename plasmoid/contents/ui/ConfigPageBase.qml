import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "../code/api.js" as Api

ScrollView {
    id: root
    property var daemonState: null
    property var settings: daemonState ? daemonState.settings : null
    signal loaded()

    contentWidth: availableWidth

    Component.onCompleted: reload()

    function reload() {
        Api.loadState(function(data, error) {
            if (data) {
                root.daemonState = data
                root.loaded()
            }
        })
    }

    function save() {
        if (!settings)
            return
        Api.saveSettings(settings, function(data, error) {
            reload()
        })
    }
}
