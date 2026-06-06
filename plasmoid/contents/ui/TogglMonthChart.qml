import QtQuick
import "Format.js" as Format

Canvas {
    id: root
    property var data: null
    height: 50

    onPaint: {
        var ctx = getContext("2d")
        ctx.clearRect(0, 0, width, height)
        if (!data)
            return
        var target = Math.max(data.targetCzk || 0, data.earnedCzk || 0)
        if (target <= 0)
            return
        var monthStart = new Date(data.monthStart)
        var monthEnd = new Date(data.monthResetsAt)
        var totalDays = Math.round((monthEnd - monthStart) / 86400000)
        if (totalDays <= 0)
            return

        var padX = 2
        var padY = 2
        function mapX(day) { return padX + day / totalDays * (width - 2 * padX) }
        function mapY(value) { return padY + (1 - value / target) * (height - 2 * padY) }

        ctx.setLineDash([4, 3])
        ctx.strokeStyle = "rgba(255,255,255,0.50)"
        ctx.lineWidth = 0.5
        ctx.beginPath()
        ctx.moveTo(padX, mapY(data.targetCzk || 0))
        ctx.lineTo(width - padX, mapY(data.targetCzk || 0))
        ctx.stroke()

        ctx.setLineDash([3, 3])
        ctx.strokeStyle = "rgba(170,170,170,0.40)"
        ctx.lineWidth = 1
        ctx.beginPath()
        ctx.moveTo(mapX(0), mapY(0))
        ctx.lineTo(mapX(totalDays), mapY(data.targetCzk || 0))
        ctx.stroke()
        ctx.setLineDash([])

        var points = [{ x: mapX(0), y: mapY(0) }]
        var cumulative = 0
        var daily = (data.dailyBreakdown || []).slice().sort(function(a, b) {
            return new Date(a.date) - new Date(b.date)
        })
        for (var i = 0; i < daily.length; i++) {
            var dayOffset = (new Date(daily[i].date) - monthStart) / 86400000
            if (dayOffset < 0)
                continue
            dayOffset = Math.min(totalDays, dayOffset)
            points.push({ x: mapX(dayOffset), y: mapY(cumulative) })
            cumulative += daily[i].earnedCzk || 0
            points.push({ x: mapX(Math.min(totalDays, dayOffset + 1)), y: mapY(cumulative) })
        }
        var nowOffset = (Date.now() - monthStart.getTime()) / 86400000
        if (nowOffset >= 0 && nowOffset <= totalDays && data.earnedCzk > 0)
            points.push({ x: mapX(nowOffset), y: mapY(data.earnedCzk) })
        if (points.length < 2)
            return

        var lineColor = data.targetCzk > 0 && data.earnedCzk >= data.targetCzk ? Style.blue : Style.green
        ctx.beginPath()
        ctx.moveTo(points[0].x, points[0].y)
        for (i = 1; i < points.length; i++)
            ctx.lineTo(points[i].x, points[i].y)
        ctx.lineTo(points[points.length - 1].x, mapY(0))
        ctx.lineTo(points[0].x, mapY(0))
        ctx.closePath()
        ctx.globalAlpha = 0.20
        ctx.fillStyle = lineColor
        ctx.fill()
        ctx.globalAlpha = 1

        ctx.beginPath()
        ctx.moveTo(points[0].x, points[0].y)
        for (i = 1; i < points.length; i++)
            ctx.lineTo(points[i].x, points[i].y)
        ctx.strokeStyle = lineColor
        ctx.lineWidth = 1.5
        ctx.stroke()
    }

    onDataChanged: requestPaint()
    onWidthChanged: requestPaint()
}
