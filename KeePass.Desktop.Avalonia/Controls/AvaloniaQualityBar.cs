using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KeePass.Desktop.Avalonia.Controls
{
	/// <summary>
	/// A custom Avalonia control that displays password quality as a
	/// gradient-filled progress bar (red → yellow → green) with a bit-count
	/// text overlay.
	///
	/// <para>
	/// Bind <see cref="QualityBits"/> to the
	/// <c>PasswordQualityBits</c> property of a ViewModel; the bar fills
	/// proportionally up to <see cref="MaxBits"/> (defaults to 128).
	/// </para>
	/// </summary>
	public sealed class AvaloniaQualityBar : Control
	{
		// ── Styled / Direct properties ────────────────────────────────────────

		public static readonly StyledProperty<uint> QualityBitsProperty =
			AvaloniaProperty.Register<AvaloniaQualityBar, uint>(
				nameof(QualityBits), defaultValue: 0);

		public static readonly StyledProperty<uint> MaxBitsProperty =
			AvaloniaProperty.Register<AvaloniaQualityBar, uint>(
				nameof(MaxBits), defaultValue: 128);

		public static readonly StyledProperty<bool> ShowTextProperty =
			AvaloniaProperty.Register<AvaloniaQualityBar, bool>(
				nameof(ShowText), defaultValue: true);

		// ── Properties ───────────────────────────────────────────────────────

		/// <summary>Estimated password strength in bits (0 = empty/unknown).</summary>
		public uint QualityBits
		{
			get => GetValue(QualityBitsProperty);
			set => SetValue(QualityBitsProperty, value);
		}

		/// <summary>Bit count at which the bar is considered full.</summary>
		public uint MaxBits
		{
			get => GetValue(MaxBitsProperty);
			set => SetValue(MaxBitsProperty, value);
		}

		/// <summary>Whether to overlay the bit count as text.</summary>
		public bool ShowText
		{
			get => GetValue(ShowTextProperty);
			set => SetValue(ShowTextProperty, value);
		}

		// ── Rendering ────────────────────────────────────────────────────────

		static AvaloniaQualityBar()
		{
			// Invalidate the visual whenever bound properties change.
			AffectsRender<AvaloniaQualityBar>(
				QualityBitsProperty, MaxBitsProperty, ShowTextProperty);
		}

		public override void Render(DrawingContext ctx)
		{
			base.Render(ctx);

			var bounds = new Rect(Bounds.Size);
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			// Background
			ctx.DrawRectangle(Brushes.LightGray, null, bounds);

			uint   bits   = QualityBits;
			uint   maxB   = Math.Max(1u, MaxBits);
			double ratio  = Math.Clamp((double)bits / maxB, 0.0, 1.0);
			double fillW  = bounds.Width * ratio;

			if (fillW > 0)
			{
				// Interpolate colour: 0% = red, 50% = yellow, 100% = green.
				Color color = InterpolateColor(ratio);
				var   brush = new SolidColorBrush(color);
				ctx.DrawRectangle(brush, null, new Rect(0, 0, fillW, bounds.Height));
			}

			// Border
			ctx.DrawRectangle(null,
				new Pen(Brushes.Gray, 1.0),
				bounds);

			// Text overlay
			if (ShowText && bounds.Height > 10)
			{
				string text = bits == 0
					? string.Empty
					: $"{bits} bits";

				if (!string.IsNullOrEmpty(text))
				{
					var formattedText = new FormattedText(
						text,
						System.Globalization.CultureInfo.CurrentCulture,
						FlowDirection.LeftToRight,
						new Typeface("Arial"),
						Math.Max(10, bounds.Height * 0.65),
						Brushes.White);

					double x = (bounds.Width  - formattedText.Width)  / 2;
					double y = (bounds.Height - formattedText.Height) / 2;
					ctx.DrawText(formattedText, new Point(x, y));
				}
			}
		}

		// ── Private helpers ───────────────────────────────────────────────────

		/// <summary>
		/// Produces a red → yellow → green colour at the given ratio [0, 1].
		/// </summary>
		private static Color InterpolateColor(double ratio)
		{
			if (ratio <= 0.5)
			{
				// Red → Yellow  (increase green from 0 → 255)
				double t = ratio * 2.0;
				byte   g = (byte)(255 * t);
				return Color.FromRgb(255, g, 0);
			}
			else
			{
				// Yellow → Green  (decrease red from 255 → 0)
				double t = (ratio - 0.5) * 2.0;
				byte   r = (byte)(255 * (1.0 - t));
				return Color.FromRgb(r, 255, 0);
			}
		}
	}
}
