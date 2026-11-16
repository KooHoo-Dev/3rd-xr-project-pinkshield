using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class BgCut
{
    // 단색 배경 누끼: 테두리에서 시작해 배경과 비슷한 픽셀(밝기 lo~hi, 채도 sMax 이하)을 따라가며 지운다.
    // 남은 가장자리 2px은 배경색과의 거리로 알파를 정하고 배경색 번짐을 걷어 낸다.
    // (wx0,wy0)-(wx1,wy1): 워터마크 상자. 이 안은 무조건 지운다(-1이면 안 씀).
    public static int Run(string src, string dst, int lo, int hi, double sMax, double edgeRange,
                          int wx0, int wy0, int wx1, int wy1, int pocketMin, int speckY, int speckMax)
    {
        var bmp = new Bitmap(src);
        int w = bmp.Width, h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[w * h * 4];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        Func<int, bool> bgLike = i =>
        {
            int b = px[i * 4], g = px[i * 4 + 1], r = px[i * 4 + 2];
            int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            if (mx < lo || mx > hi) return false;
            return mx == 0 || (mx - mn) / (double)mx <= sMax;
        };

        // 배경색 = 테두리 픽셀 평균
        double br = 0, bg = 0, bb = 0; int bn = 0;
        for (int x = 0; x < w; x++) foreach (int y in new[] { 0, h - 1 }) { int i = y * w + x; bb += px[i * 4]; bg += px[i * 4 + 1]; br += px[i * 4 + 2]; bn++; }
        br /= bn; bg /= bn; bb /= bn;

        var removed = new bool[w * h];
        var q = new Queue<int>();
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
        {
            if (x != 0 && y != 0 && x != w - 1 && y != h - 1) continue;
            int i = y * w + x;
            if (bgLike(i)) { removed[i] = true; q.Enqueue(i); }
        }
        Action<int> Try = j => { if (!removed[j] && bgLike(j)) { removed[j] = true; q.Enqueue(j); } };
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = i % w, y = i / w;
            if (x > 0) Try(i - 1); if (x < w - 1) Try(i + 1); if (y > 0) Try(i - w); if (y < h - 1) Try(i + w);
        }

        // 테두리와 이어지지 않은 배경 덩어리(회오리 틈 등)도 pocketMin 픽셀 이상이면 지운다
        if (pocketMin > 0)
        {
            var seen = new bool[w * h];
            for (int s = 0; s < w * h; s++)
            {
                if (seen[s] || removed[s] || !bgLike(s)) continue;
                var comp = new List<int>(); var cq = new Queue<int>(); cq.Enqueue(s); seen[s] = true;
                while (cq.Count > 0)
                {
                    int i = cq.Dequeue(); comp.Add(i); int x = i % w, y = i / w;
                    int[] nb = { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 };
                    foreach (int j in nb) if (j >= 0 && !seen[j] && !removed[j] && bgLike(j)) { seen[j] = true; cq.Enqueue(j); }
                }
                if (comp.Count >= pocketMin) foreach (int i in comp) removed[i] = true;
            }
        }
        // speckY 아래쪽의 작은 무채색 점(speckMax 픽셀 이하) 제거
        if (speckY >= 0)
        {
            var seen = new bool[w * h];
            for (int s = speckY * w; s < w * h; s++)
            {
                if (seen[s] || removed[s] || px[s * 4 + 3] == 0) continue;
                var comp = new List<int>(); var cq = new Queue<int>(); cq.Enqueue(s); seen[s] = true; bool gray = true;
                while (cq.Count > 0)
                {
                    int i = cq.Dequeue(); comp.Add(i); int x = i % w, y = i / w;
                    int b = px[i * 4], g = px[i * 4 + 1], r = px[i * 4 + 2];
                    int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                    if (mx > 0 && (mx - mn) / (double)mx > 0.12) gray = false;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (!seen[j] && !removed[j] && px[j * 4 + 3] > 0) { seen[j] = true; cq.Enqueue(j); }
                    }
                    if (comp.Count > speckMax) break;
                }
                if (comp.Count <= speckMax && gray) foreach (int i in comp) removed[i] = true;
            }
        }

        if (wx0 >= 0)
            for (int y = wy0; y <= wy1; y++) for (int x = wx0; x <= wx1; x++) removed[y * w + x] = true;

        // 가장자리 2px: 알파와 색 보정
        var dist = new int[w * h];
        for (int i = 0; i < w * h; i++) dist[i] = removed[i] ? 0 : 99;
        for (int pass = 1; pass <= 2; pass++)
            for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (dist[i] != 99) continue;
                if (dist[i - 1] == pass - 1 || dist[i + 1] == pass - 1 || dist[i - w] == pass - 1 || dist[i + w] == pass - 1) dist[i] = pass;
            }
        int count = 0;
        for (int i = 0; i < w * h; i++)
        {
            if (removed[i]) { px[i * 4 + 3] = 0; count++; continue; }
            if (dist[i] > 2) continue;
            double r = px[i * 4 + 2], g = px[i * 4 + 1], b = px[i * 4];
            double d = Math.Sqrt((r - br) * (r - br) + (g - bg) * (g - bg) + (b - bb) * (b - bb));
            double a = Math.Min(1.0, d / edgeRange);
            if (a < 0.08) { px[i * 4 + 3] = 0; continue; }
            px[i * 4 + 2] = Clamp((r - br * (1 - a)) / a);
            px[i * 4 + 1] = Clamp((g - bg * (1 - a)) / a);
            px[i * 4] = Clamp((b - bb * (1 - a)) / a);
            px[i * 4 + 3] = (byte)Math.Min(px[i * 4 + 3], (int)(a * 255));
        }

        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        bmp.Save(dst, ImageFormat.Png);
        bmp.Dispose();
        return count;
    }

    static byte Clamp(double v) { return (byte)Math.Max(0, Math.Min(255, Math.Round(v))); }
}
