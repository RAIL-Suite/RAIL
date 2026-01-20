using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AgentTest.Windows
{
    public partial class DrawingWindow : Window
    {
        public DrawingWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws points provided by LLM
        /// LLM generates coordinates for any shape it wants to create
        /// </summary>
        public void executeDP(List<Dictionary<string, double>> points, string colorName, double thickness)
        {
            StatusText.Text = $"Disegno {points.Count} punti...";

            // Convert color name to Color
            var color = ConvertColor(colorName);

            // Convert Dictionary points to StylusPointCollection
            var stylusPoints = new StylusPointCollection();
            foreach (var point in points)
            {
                if (point.ContainsKey("x") && point.ContainsKey("y"))
                {
                    stylusPoints.Add(new StylusPoint(point["x"], point["y"]));
                }
            }

            if (stylusPoints.Count > 0)
            {
                AnimateStroke(stylusPoints, color, thickness);
            }

            StatusText.Text = $"✓ Disegnati {points.Count} punti";
        }

        public void Clear()
        {
            DrawingCanvas.Strokes.Clear();
            StatusText.Text = "Canvas pulito";
        }

        private Color ConvertColor(string colorName)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(colorName);
            }
            catch
            {
                return Colors.Black; // Default
            }
        }

        private async void AnimateStroke(StylusPointCollection points, Color color, double thickness, int delayMs = 0)
        {
            if (points == null || points.Count == 0)
                return;

            if (delayMs > 0)
                await Task.Delay(delayMs);

            // Create stroke with first point (WPF requires at least one point)
            var initialCollection = new StylusPointCollection();
            initialCollection.Add(points[0]);

            var stroke = new Stroke(initialCollection);
            stroke.DrawingAttributes.Color = color;
            stroke.DrawingAttributes.Width = thickness;
            stroke.DrawingAttributes.Height = thickness;

            DrawingCanvas.Strokes.Add(stroke);

            // Animate adding remaining points progressively
            for (int i = 1; i < points.Count; i++)
            {
                stroke.StylusPoints.Add(points[i]);
                await Task.Delay(5); // 5ms between points for smooth animation
            }
        }
    }
}







