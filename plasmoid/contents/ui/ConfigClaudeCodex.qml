import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "../code/api.js" as Api

ConfigPageBase {
    id: root

    ColumnLayout {
        width: root.availableWidth
        spacing: 12

        GroupBox {
            title: i18n("Credentials")
            Layout.fillWidth: true
            ColumnLayout {
                anchors.fill: parent
                Repeater {
                    model: root.state && root.state.accounts ? root.state.accounts : []
                    delegate: Label {
                        Layout.fillWidth: true
                        text: modelData.service + ": " + (modelData.credentialPath || i18n("No credentials"))
                        elide: Text.ElideMiddle
                    }
                }
                Button {
                    text: i18n("Refresh Claude / Codex")
                    onClicked: Api.refresh("accounts", function() { root.reload() })
                }
            }
        }

        GroupBox {
            title: i18n("Chart time window (hours)")
            Layout.fillWidth: true
            ColumnLayout {
                anchors.fill: parent
                Repeater {
                    model: root.state && root.state.knownLabels ? root.state.knownLabels : []
                    delegate: RowLayout {
                        Layout.fillWidth: true
                        CheckBox {
                            checked: root.settings && root.settings.hiddenLimits ? root.settings.hiddenLimits.indexOf(modelData.label) < 0 : true
                            onToggled: {
                                if (!root.settings.hiddenLimits)
                                    root.settings.hiddenLimits = []
                                var idx = root.settings.hiddenLimits.indexOf(modelData.label)
                                if (checked && idx >= 0)
                                    root.settings.hiddenLimits.splice(idx, 1)
                                if (!checked && idx < 0)
                                    root.settings.hiddenLimits.push(modelData.label)
                                root.save()
                            }
                        }
                        Label { Layout.fillWidth: true; text: modelData.display; elide: Text.ElideRight }
                        TextField {
                            Layout.preferredWidth: 70
                            horizontalAlignment: TextInput.AlignRight
                            text: root.settings && root.settings.chartWindowHours && root.settings.chartWindowHours[modelData.label]
                                ? root.settings.chartWindowHours[modelData.label] : defaultHours(modelData.label)
                            onEditingFinished: {
                                var v = parseFloat(text)
                                if (v > 0) {
                                    if (!root.settings.chartWindowHours)
                                        root.settings.chartWindowHours = {}
                                    root.settings.chartWindowHours[modelData.label] = v
                                    root.save()
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    function defaultHours(label) {
        label = (label || "").toLowerCase()
        if (label.indexOf("5h") >= 0) return "48"
        if (label.indexOf("review") >= 0) return "168"
        return "336"
    }
}
