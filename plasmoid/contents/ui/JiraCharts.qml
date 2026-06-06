import QtQuick

Column {
    id: root
    property var data: null
    property var history: []
    spacing: 0

    Text {
        text: "Done per day (last 30d)"
        color: Style.muted
        font.pixelSize: 8
        height: 13
    }

    Canvas {
        id: doneChart
        width: parent.width
        height: 40
        onPaint: {
            var ctx = getContext("2d")
            ctx.clearRect(0, 0, width, height)
            var hist = root.history || []
            if (hist.length < 2)
                return
            var byDate = {}
            for (var i = 1; i < hist.length; i++) {
                var delta = Math.max(0, (hist[i].myDoneIssues || 0) - (hist[i - 1].myDoneIssues || 0))
                byDate[hist[i].date] = delta
            }
            var maxVal = 1
            for (var k in byDate)
                maxVal = Math.max(maxVal, byDate[k])
            var padX = 2
            var padY = 2
            var colW = (width - 2 * padX) / 30
            var start = new Date()
            start.setDate(start.getDate() - 29)
            for (i = 0; i < 30; i++) {
                var d = new Date(start)
                d.setDate(start.getDate() + i)
                var key = d.toISOString().slice(0, 10)
                var val = byDate[key] || 0
                if (val <= 0)
                    continue
                var h = (height - 2 * padY) * val / maxVal
                ctx.fillStyle = val >= 3 ? Style.green : Style.blue
                ctx.fillRect(padX + i * colW, height - padY - h, Math.max(1, colW - 1), h)
            }
        }
    }

    Text {
        text: "Rank trend (lower = better)"
        color: Style.muted
        font.pixelSize: 8
        height: 13
        visible: rankChart.visible
    }

    Canvas {
        id: rankChart
        width: parent.width
        height: 30
        visible: (root.history || []).length >= 2
        onPaint: {
            var ctx = getContext("2d")
            ctx.clearRect(0, 0, width, height)
            var ranks = []
            var hist = root.history || []
            for (var i = 0; i < hist.length; i++) {
                if (hist[i].myRank > 0 && hist[i].rankingSize > 1)
                    ranks.push(hist[i])
            }
            if (ranks.length < 2)
                return
            var padX = 2
            var padY = 2
            var minDate = new Date(ranks[0].date).getTime()
            var maxDate = new Date(ranks[ranks.length - 1].date).getTime()
            var span = Math.max(1, maxDate - minDate)
            var maxRank = 1
            for (i = 0; i < ranks.length; i++)
                maxRank = Math.max(maxRank, ranks[i].rankingSize)
            function x(r) { return padX + (new Date(r.date).getTime() - minDate) / span * (width - 2 * padX) }
            function y(r) { return padY + (r.myRank - 1) / Math.max(1, maxRank - 1) * (height - 2 * padY) }

            ctx.setLineDash([3, 3])
            ctx.strokeStyle = "rgba(255,193,7,0.25)"
            ctx.lineWidth = 0.5
            ctx.beginPath()
            ctx.moveTo(padX, padY)
            ctx.lineTo(width - padX, padY)
            ctx.stroke()
            ctx.setLineDash([])

            ctx.strokeStyle = Style.blue
            ctx.lineWidth = 1.5
            ctx.beginPath()
            ctx.moveTo(x(ranks[0]), y(ranks[0]))
            for (i = 1; i < ranks.length; i++)
                ctx.lineTo(x(ranks[i]), y(ranks[i]))
            ctx.stroke()

            var last = ranks[ranks.length - 1]
            ctx.fillStyle = Style.gold
            ctx.beginPath()
            ctx.arc(x(last), y(last), 2, 0, Math.PI * 2)
            ctx.fill()
        }
    }

    onHistoryChanged: {
        doneChart.requestPaint()
        rankChart.requestPaint()
    }
}
