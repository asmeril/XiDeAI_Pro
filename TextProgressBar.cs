using System;
using System.Drawing;
using System.Windows.Forms;

namespace XiDeAI_Pro
{
    /// <summary>
    /// Metin gösterebilen özelleştirilmiş Progress Bar kontrolü.
    /// </summary>
    public class TextProgressBar : Control
    {
        private int _value;
        private int _maximum = 100;
        private Color _barColor = Color.Green;
        private string _customText = "";
        
        // Metin renkleri
        private Color _textColor = Color.White;
        private Color _shadowColor = Color.Black;

        public TextProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | 
                          ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.ResizeRedraw |
                          ControlStyles.SupportsTransparentBackColor, true);
            
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        }

        public int Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    if (value < 0) value = 0;
                    if (value > _maximum) value = _maximum;
                    _value = value;
                    this.Invalidate();
                }
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    if (_value > _maximum) _value = _maximum;
                    this.Invalidate();
                }
            }
        }

        public Color BarColor
        {
            get => _barColor;
            set
            {
                if (_barColor != value)
                {
                    _barColor = value;
                    this.Invalidate();
                }
            }
        }

        public string CustomText
        {
            get => _customText;
            set
            {
                if (_customText != value)
                {
                    _customText = value;
                    this.Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Arka plan
            using (var bgBrush = new SolidBrush(this.BackColor))
            {
                e.Graphics.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Bar
            if (_maximum > 0 && _value > 0)
            {
                float percent = (float)_value / _maximum;
                percent = Math.Max(0, Math.Min(1, percent));
                int width = (int)(this.Width * percent);
                
                if (width > 0)
                {
                    using (var barBrush = new SolidBrush(_barColor))
                    {
                        e.Graphics.FillRectangle(barBrush, 0, 0, width, this.Height);
                    }
                }
            }

            // Metin
            if (!string.IsNullOrEmpty(_customText))
            {
                Rectangle textRect = this.ClientRectangle;
                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | 
                                      TextFormatFlags.VerticalCenter | 
                                      TextFormatFlags.SingleLine |
                                      TextFormatFlags.NoPadding;

                // Gölge
                Rectangle shadowRect = new Rectangle(textRect.X + 1, textRect.Y + 1, textRect.Width, textRect.Height);
                TextRenderer.DrawText(e.Graphics, _customText, this.Font, shadowRect, _shadowColor, Color.Transparent, flags);

                // Ana Metin
                TextRenderer.DrawText(e.Graphics, _customText, this.Font, textRect, this.ForeColor, Color.Transparent, flags);
            }
            
            // Çerçeve
            using (var borderPen = new Pen(Color.FromArgb(80, 80, 80)))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
        
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}

