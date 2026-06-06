import QtQuick
import org.kde.plasma.configuration

ConfigModel {
    ConfigCategory {
        name: i18n("General")
        icon: "configure"
        source: "../ui/ConfigGeneral.qml"
    }
    ConfigCategory {
        name: i18n("Claude / Codex")
        icon: "applications-development"
        source: "../ui/ConfigClaudeCodex.qml"
    }
    ConfigCategory {
        name: i18n("Toggl Track")
        icon: "view-time-schedule"
        source: "../ui/ConfigToggl.qml"
    }
    ConfigCategory {
        name: i18n("JIRA")
        icon: "view-task"
        source: "../ui/ConfigJira.qml"
    }
}
