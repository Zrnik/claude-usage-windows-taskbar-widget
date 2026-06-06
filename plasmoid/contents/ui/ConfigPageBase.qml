import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "../code/api.js" as Api

ScrollView {
    id: root
    property var state: null
    property var settings: state ? state.settings : null
    signal loaded()

    contentWidth: availableWidth

    Component.onCompleted: reload()

    function reload() {
        Api.loadState(function(data, error) {
            if (data) {
                root.state = data
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
