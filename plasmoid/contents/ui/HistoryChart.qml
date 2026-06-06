import QtQuick
import "Format.js" as Format

Canvas {
    id: root
    property var records: []
    property string label: ""
    property var overrides: ({})
    height: 40

    onPaint: {
        var ctx = getContext("2d")
        ctx.clearRect(0, 0, width, height)
        if (!records || records.length < 2)
            return

        var hours = Format.historyWindowHours(label, overrides)
        var end = Date.now()
        var start = end - hours * 3600000
        var raw = []
        for (var i = 0; i < records.length; i++) {
            if (!records[i].limits || records[i].limits[label] === undefined)
                continue
            var ts = new Date(records[i].timestamp).getTime()
            if (ts >= start)
                raw.push({ ts: ts, value: records[i].limits[label] })
        }
        if (raw.length < 2)
            return
        raw.sort(function(a, b) { return a.ts - b.ts })

        var points = []
        for (i = 0; i < raw.length; i++) {
            points.push(raw[i])
            if (i < raw.length - 1 && raw[i + 1].ts - raw[i].ts >= 7200000)
                points.push({ ts: raw[i + 1].ts - 1000, value: raw[i].value })
        }

        var maxValue = 100
        for (i = 0; i < points.length; i++)
            maxValue = Math.max(maxValue, points[i].value)
        if (maxValue > 100)
            maxValue = Math.ceil(maxValue / 10) * 10

        var padX = 2
        var padY = 2
        function mapX(ts) { return padX + (ts - start) / (end - start) * (width - 2 * padX) }
        function mapY(v) { return padY + (1 - v / maxValue) * (height - 2 * padY) }
        function color(v) { return Format.barColor(v) }

        for (var ref = 25; ref <= maxValue; ref += 25) {
            var y = mapY(ref)
            ctx.strokeStyle = ref === 100 ? "rgba(255,255,255,0.50)" : "rgba(255,255,255,0.19)"
            ctx.setLineDash([4, 3])
            ctx.lineWidth = 0.5
            ctx.beginPath()
            ctx.moveTo(padX, y)
            ctx.lineTo(width - padX, y)
            ctx.stroke()
        }
        ctx.setLineDash([])

        var startIndex = 0
        while (startIndex < points.length - 1) {
            var segColor = color(points[startIndex].value)
            var endIndex = startIndex + 1
            while (endIndex < points.length && color(points[endIndex].value) === segColor)
                endIndex++
            if (endIndex < points.length)
                endIndex++

            ctx.beginPath()
            ctx.moveTo(mapX(points[startIndex].ts), mapY(points[startIndex].value))
            for (i = startIndex + 1; i < endIndex; i++)
                ctx.lineTo(mapX(points[i].ts), mapY(points[i].value))
            ctx.lineTo(mapX(points[endIndex - 1].ts), height - padY)
            ctx.lineTo(mapX(points[startIndex].ts), height - padY)
            ctx.closePath()
            ctx.fillStyle = segColor.replace("#", "rgba(") // fallback below if unsupported
            ctx.globalAlpha = 0.20
            ctx.fillStyle = segColor
            ctx.fill()
            ctx.globalAlpha = 1

            ctx.beginPath()
            ctx.moveTo(mapX(points[startIndex].ts), mapY(points[startIndex].value))
            for (i = startIndex + 1; i < endIndex; i++)
                ctx.lineTo(mapX(points[i].ts), mapY(points[i].value))
            ctx.strokeStyle = segColor
            ctx.lineWidth = 1.5
            ctx.stroke()
            startIndex = endIndex - 1
        }
    }

    onRecordsChanged: requestPaint()
    onLabelChanged: requestPaint()
    onOverridesChanged: requestPaint()
    onWidthChanged: requestPaint()
}
