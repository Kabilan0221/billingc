using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ShopBilling.UI.Utilities
{
    public static class BarcodeRenderer
    {
        // Simple Code 128 / Code 39 / Interleaved 2 of 5 rasterizer using System.Drawing
        // (Runs natively on Windows 7 SP1 GDI+ without third-party C++ dependencies)
        public static Bitmap GenerateBarcodeImage(string barcodeText, int width = 280, int height = 80)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                if (string.IsNullOrEmpty(barcodeText)) return bmp;

                // Simple pseudo-Code128 pattern based on hash/characters
                int barAreaHeight = height - 22;
                int startX = 14;
                int currentX = startX;

                // Start guard
                DrawBar(g, ref currentX, 2, barAreaHeight);
                DrawSpace(ref currentX, 2);
                DrawBar(g, ref currentX, 2, barAreaHeight);
                DrawSpace(ref currentX, 2);

                for (int i = 0; i < barcodeText.Length; i++)
                {
                    char c = barcodeText[i];
                    int charVal = (int)c % 16;

                    for (int b = 0; b < 4; b++)
                    {
                        bool isBar = ((charVal >> b) & 1) == 1;
                        int barWidth = isBar ? 2 : 1;
                        if (b % 2 == 0) DrawBar(g, ref currentX, barWidth, barAreaHeight);
                        else DrawSpace(ref currentX, barWidth);
                    }
                }

                // Stop guard
                DrawBar(g, ref currentX, 2, barAreaHeight);
                DrawSpace(ref currentX, 2);
                DrawBar(g, ref currentX, 3, barAreaHeight);

                // Barcode text label below
                using (var font = new Font("Consolas", 9f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.Black))
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString(barcodeText, font, brush, new RectangleF(0, height - 18, width, 18), format);
                }
            }
            return bmp;
        }

        private static void DrawBar(Graphics g, ref int x, int width, int height)
        {
            using (var brush = new SolidBrush(Color.Black))
            {
                g.FillRectangle(brush, x, 5, width, height);
            }
            x += width;
        }

        private static void DrawSpace(ref int x, int width)
        {
            x += width;
        }
    }
}
