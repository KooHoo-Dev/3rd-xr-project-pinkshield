using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ShadowCut
{
    // 아래쪽 끝에서 시작해 채도 낮은(회색) 픽셀만 따라가며 지운다. 반환: 지운 픽셀 수
    public static int Run(string src, string dst, double satMax, int lumMin, int lumMax, int band, int extraSeeds, double satLow, int px0, int py0, int px1, int py1)
    {
        var bmp = new Bitmap(src);
        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[w * h * 4];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        Func<int, bool> opaque = i => px[i * 4 + 3] > 16;
        Func<int, bool> shadowLike = i =>
        {
            int b = px[i * 4], g = px[i * 4 + 1], r = px[i * 4 + 2];
            int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            if (mx < lumMin) return false;
            int xx = i % w, yy = i / w;
            if (xx >= px0 && xx <= px1 && yy >= py0 && yy <= py1) return false; // 보호 구역
            double sat = (mx - mn) / (double)mx;
            if (sat <= satLow) return true;
            return mx <= lumMax && sat <= satMax;
        };

        // 불투명 픽셀이 20개 이상인 가장 아래 줄 (흩어진 점은 무시)
        int yMax = -1;
        for (int y = h - 1; y >= 0 && yMax < 0; y--)
        {
            int cnt = 0;
            for (int x = 0; x < w; x++) if (opaque(y * w + x)) cnt++;
            if (cnt >= 20) yMax = y;
        }
        if (yMax < 0) { bmp.UnlockBits(data); bmp.Dispose(); return 0; }
        int yLimit = yMax - band;

        var removed = new bool[w * h];
        var q = new Queue<int>();
        // 씨앗: 맨 아래 몇 줄의 회색 픽셀
        for (int y = yMax; y >= Math.Max(0, yMax - extraSeeds); y--)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (opaque(i) && shadowLike(i) && !removed[i]) { removed[i] = true; q.Enqueue(i); }
            }
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = i % w, y = i / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < yLimit || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (removed[j] || !opaque(j) || !shadowLike(j)) continue;
                    removed[j] = true; q.Enqueue(j);
                }
        }
        // 지운 영역 바로 옆의 반투명 가장자리 찌꺼기 정리
        var fringe = new List<int>();
        for (int y = Math.Max(1, yLimit); y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (removed[i] || px[i * 4 + 3] == 0) continue;
                if (px[i * 4 + 3] > 200) continue;
                if (removed[i - 1] || removed[i + 1] || removed[i - w] || removed[i + w]) fringe.Add(i);
            }
        foreach (int i in fringe) removed[i] = true;

        int count = 0;
        for (int i = 0; i < w * h; i++) if (removed[i]) { px[i * 4 + 3] = 0; count++; }

        // 떨어져 남은 납작한 조각(그림자 테두리 잔재) 제거
        var seen = new bool[w * h];
        for (int s = yLimit * w; s < w * h; s++)
        {
            if (s < 0 || seen[s] || px[s * 4 + 3] <= 4) continue;
            var comp = new List<int>(); var cq = new Queue<int>(); cq.Enqueue(s); seen[s] = true;
            int minX = w, maxX = 0, minY = h, maxY = 0; bool touchesAbove = false;
            while (cq.Count > 0)
            {
                int i = cq.Dequeue(); comp.Add(i); int x = i % w, y = i / w;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny >= h || ny < 0) continue;
                    int j = ny * w + nx;
                    if (seen[j] || px[j * 4 + 3] <= 4) continue;
                    if (ny < yLimit) { touchesAbove = true; continue; }
                    seen[j] = true; cq.Enqueue(j);
                }
            }
            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            long aSum = 0; foreach (int i in comp) aSum += px[i * 4 + 3];
            bool faint = aSum / comp.Count < 110;
            // 바닥 근처에 흩어진 흰 점(누끼 도구가 남긴 그림자 테두리)
            if (!faint && comp.Count <= 40 && minY > yMax - 80)
            {
                bool allWhite = true;
                foreach (int i in comp)
                {
                    int b = px[i * 4], g = px[i * 4 + 1], r = px[i * 4 + 2];
                    if (Math.Min(r, Math.Min(g, b)) < 225) { allWhite = false; break; }
                }
                faint = allWhite;
            }
            if (!touchesAbove && (faint || (cw >= 60 && ch <= 70 && ch * 3 < cw)))
                foreach (int i in comp) { px[i * 4 + 3] = 0; removed[i] = true; count++; }
        }
        // 새로 생긴 경계를 1px 부드럽게: 지운 픽셀과 맞닿은 불투명 픽셀은 알파 절반
        for (int y = Math.Max(1, yLimit); y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (removed[i] || px[i * 4 + 3] == 0) continue;
                if (removed[i - 1] || removed[i + 1] || removed[i - w] || removed[i + w])
                    px[i * 4 + 3] = (byte)(px[i * 4 + 3] / 2);
            }

        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        bmp.Save(dst, ImageFormat.Png);
        bmp.Dispose();
        return count;
    }
}
