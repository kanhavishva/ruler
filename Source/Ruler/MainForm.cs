using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace Ruler
{
	sealed public class MainForm : Form, IRulerInfo
	{
		#region ResizeRegion enum

		private enum ResizeRegion
		{
			None, N, NE, E, SE, S, SW, W, NW
		}

		#endregion ResizeRegion enum

		#region Fields

		private ToolTip toolTip;
		private Point offset;
		private Rectangle mouseDownRect;
		private int resizeBorderWidth;
		private Point mouseDownPoint;
		private ResizeRegion resizeRegion;
		private ContextMenu contextMenu;
		private List<MenuItemHolder> menuItemList;

		private bool isVertical;
		private bool isLocked;
		private bool showToolTip;

		private readonly RulerInfo initRulerInfo;

		#endregion

		#region Init

		[STAThread]
		private static void Main(params string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			MainForm mainForm;

			if (args.Length == 0)
			{
				mainForm = new MainForm();
			}
			else
			{
				mainForm = new MainForm(RulerInfo.CovertToRulerInfo(args));
			}

			Application.Run(mainForm);
		}

		public MainForm()
			:this(RulerInfo.GetDefaultRulerInfo())
		{
		}		

		public MainForm(RulerInfo rulerInfo)
		{
			this.initRulerInfo = rulerInfo;			
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			this.Init(this.initRulerInfo);
		}

		private void Init(RulerInfo rulerInfo)
		{
			// Set fields
			this.toolTip = new ToolTip();
			this.toolTip.AutoPopDelay = 10000;
			this.toolTip.InitialDelay = 1;

			this.resizeRegion = ResizeRegion.None;
			this.contextMenu = new ContextMenu();
			this.resizeBorderWidth = 5;

			// Form setup ------------------
			this.SetStyle(ControlStyles.ResizeRedraw, true);
			this.UpdateStyles();

			ResourceManager resources = new ResourceManager(typeof(MainForm));
			this.Icon = ((Icon)(resources.GetObject("$this.Icon")));
			this.Opacity = rulerInfo.Opacity;
			this.FormBorderStyle = FormBorderStyle.None;
			this.Font = new Font("Tahoma", 10);
			this.Text = "Ruler";
			this.BackColor = Color.White;

			// Create menu
			this.CreateMenuItems(rulerInfo);		

			RulerInfo.CopyInto(rulerInfo, this);			

			this.ContextMenu = contextMenu;

			this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
		}

		private void CreateMenuItems(RulerInfo rulerInfo)
		{
			var list = new List<MenuItemHolder>()
			{
				new MenuItemHolder(MenuItemEnum.TopMost, "Stay On Top", this.TopMostHandler, rulerInfo.TopMost),
				new MenuItemHolder(MenuItemEnum.Vertical, "Vertical", this.VerticalHandler, rulerInfo.IsVertical),
				new MenuItemHolder(MenuItemEnum.ShowToolTip, "Tool Tip", this.ShowToolTipHandler, rulerInfo.ShowToolTip),
				new MenuItemHolder(MenuItemEnum.Opacity, "Opacity", null, false),
				new MenuItemHolder(MenuItemEnum.LockResize, "Lock Resizing", this.LockResizeHandler, rulerInfo.IsLocked),
				new MenuItemHolder(MenuItemEnum.SetSize, "Set size...", this.SetSizeHandler, false),
				new MenuItemHolder(MenuItemEnum.Duplicate, "Duplicate", this.DuplicateHandler, false),
				MenuItemHolder.Separator,
				new MenuItemHolder(MenuItemEnum.About, "About...", this.AboutHandler, false),
				MenuItemHolder.Separator,
#if DEBUG
				new MenuItemHolder(MenuItemEnum.RulerInfo, "Get RulerInfo", (s, ea) => {
					string parameters = this.GetRulerInfo().ConvertToParameters();
					Clipboard.SetText(parameters);
					MessageBox.Show(string.Concat("Copied to clipboard:", Environment.NewLine, parameters));
				}, false),
				MenuItemHolder.Separator,
#endif
				new MenuItemHolder(MenuItemEnum.Exit, "Exit", this.ExitHandler, false)
			};
			
			// Build opacity menu
			MenuItem opacityMenuItem = list.Find(m => m.MenuItemEnum == MenuItemEnum.Opacity).MenuItem;

			for (int i = 10; i <= 100; i += 10)
			{
				MenuItem subMenu = new MenuItem(i + "%", this.OpacityMenuHandler);
				subMenu.Checked = i == rulerInfo.Opacity * 100;
				opacityMenuItem.MenuItems.Add(subMenu);
			}

			// Build main context menu
			list.ForEach(mh => this.contextMenu.MenuItems.Add(mh.MenuItem));

			this.menuItemList = list;
		}

		#endregion

		#region Properties

		public bool IsVertical
		{
			get { return this.isVertical; }
			set
			{
				this.isVertical = value;
				this.UpdateMenuItem(MenuItemEnum.Vertical, value);				
			}
		}

		public bool IsLocked
		{
			get { return this.isLocked; }
			set
			{
				this.isLocked = value;
				this.UpdateMenuItem(MenuItemEnum.LockResize, value);
			}
		}

		public bool ShowToolTip
		{
			get { return this.showToolTip; }
			set
			{
				this.showToolTip = value;
				this.UpdateMenuItem(MenuItemEnum.ShowToolTip, value);

				if (value)
				{
					this.SetToolTip();
				}
				else
				{
					this.RemoveToolTip();
				}
			}
		}

#endregion

		#region Helpers

		private RulerInfo GetRulerInfo()
		{
			RulerInfo rulerInfo = new RulerInfo();

			RulerInfo.CopyInto(this, rulerInfo);

			return rulerInfo;
		}

		private MenuItemHolder FindMenuItem(MenuItemEnum menuItemEnum)
		{
			return this.menuItemList.Find(mih => mih.MenuItemEnum == menuItemEnum);
		}

		private void UpdateMenuItem(MenuItemEnum menuItemEnum, bool isChecked)
		{
			MenuItemHolder menuItemHolder = this.FindMenuItem(menuItemEnum);

			if (menuItemHolder != null)
			{
				menuItemHolder.MenuItem.Checked = isChecked;
			}
		}

		private void ChangeOrientation()
		{
			this.IsVertical = !this.IsVertical;
			int width = Width;
			this.Width = Height;
			this.Height = width;
		}

		private void SetToolTip()
		{
			this.toolTip.SetToolTip(this, string.Format("Width: {0} pixels\nHeight: {1} pixels", this.Width, this.Height));
		}

		private void RemoveToolTip()
		{
			this.toolTip.RemoveAll();
		}

#endregion

		#region Menu Item Handlers

		private void SetSizeHandler(object sender, EventArgs e)
		{
			SetSizeForm form = new SetSizeForm(this.Width, this.Height);

			if (this.TopMost)
			{
				form.TopMost = true;
			}

			if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Size size = form.GetNewSize();

				this.Width = size.Width;
				this.Height = size.Height;
			}
		}

		private void LockResizeHandler(object sender, EventArgs e)
		{
			this.IsLocked = !this.IsLocked;			
		}

		private void DuplicateHandler(object sender, EventArgs e)
		{
			string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;

			RulerInfo rulerInfo = this.GetRulerInfo();

			ProcessStartInfo startInfo = new ProcessStartInfo(exe, rulerInfo.ConvertToParameters());

			Process process = new Process
			{
				StartInfo = startInfo
			};

			process.Start();
		}

		private void OpacityMenuHandler(object sender, EventArgs e)
		{
			MenuItem opacityMenuItem = (MenuItem)sender;

			foreach (MenuItem menuItem in opacityMenuItem.Parent.MenuItems)
			{
				menuItem.Checked = false;
			}

			opacityMenuItem.Checked = true;
			this.Opacity = double.Parse(opacityMenuItem.Text.Replace("%", "")) / 100;
		}

		private void ShowToolTipHandler(object sender, EventArgs e)
		{
			this.ShowToolTip = !this.ShowToolTip;
		}

		private void ExitHandler(object sender, EventArgs e)
		{
			this.Close();
		}

		private void VerticalHandler(object sender, EventArgs e)
		{
			this.ChangeOrientation();
		}

		private void TopMostHandler(object sender, EventArgs e)
		{
			MenuItem mi = (MenuItem)sender;

			mi.Checked = !mi.Checked;
			this.TopMost = mi.Checked;
		}

		private void AboutHandler(object sender, EventArgs e)
		{
			string message = string.Format("Original Ruler implemented by Jeff Key\nwww.sliver.com\nruler.codeplex.com\nIcon by Kristen Magee @ www.kbecca.com.\nMaintained by Andrija Cacanovic\nHosted on \nhttps://github.com/andrijac/ruler", Application.ProductVersion);
			MessageBox.Show(message, "About Ruler", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

#endregion

		#region Input

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);

			if (e.Button == MouseButtons.Left)
			{
				this.ChangeOrientation();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			this.offset = new Point(Control.MousePosition.X - this.Location.X, Control.MousePosition.Y - this.Location.Y);
			this.mouseDownPoint = Control.MousePosition;
			this.mouseDownRect = this.ClientRectangle;

			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			this.resizeRegion = ResizeRegion.None;
			base.OnMouseUp(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (this.resizeRegion != ResizeRegion.None)
			{
				this.HandleResize();
				return;
			}

			Point clientCursorPos = this.PointToClient(MousePosition);
			Rectangle resizeInnerRect = this.ClientRectangle;
			resizeInnerRect.Inflate(-resizeBorderWidth, -resizeBorderWidth);

			bool inResizableArea = this.ClientRectangle.Contains(clientCursorPos) && !resizeInnerRect.Contains(clientCursorPos);

			if (inResizableArea)
			{
				ResizeRegion resizeRegion = this.GetResizeRegion(clientCursorPos);
				this.SetResizeCursor(resizeRegion);

				if (e.Button == MouseButtons.Left)
				{
					this.resizeRegion = resizeRegion;
					this.HandleResize();
				}
			}
			else
			{
				this.Cursor = Cursors.Default;

				if (e.Button == MouseButtons.Left)
				{
					this.Location = new Point(Control.MousePosition.X - offset.X, Control.MousePosition.Y - offset.Y);
				}
			}

			base.OnMouseMove(e);
		}

		protected override void OnResize(EventArgs e)
		{
			// ToolTip needs to be set again on resize to refresh new size values inside ToolTip
			if (this.ShowToolTip)
			{
				this.SetToolTip();
			}

			base.OnResize(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Right:
				case Keys.Left:
				case Keys.Up:
				case Keys.Down:
					this.HandleMoveResizeKeystroke(e);
					break;

				case Keys.Space:
					this.ChangeOrientation();
					break;
			}

			base.OnKeyDown(e);
		}

		private void HandleMoveResizeKeystroke(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Right)
			{
				if (e.Control)
				{
					if (e.Shift)
					{
						this.Width += 1;
					}
					else
					{
						this.Left += 1;
					}
				}
				else
				{
					this.Left += 5;
				}
			}
			else if (e.KeyCode == Keys.Left)
			{
				if (e.Control)
				{
					if (e.Shift)
					{
						this.Width -= 1;
					}
					else
					{
						this.Left -= 1;
					}
				}
				else
				{
					this.Left -= 5;
				}
			}
			else if (e.KeyCode == Keys.Up)
			{
				if (e.Control)
				{
					if (e.Shift)
					{
						this.Height -= 1;
					}
					else
					{
						this.Top -= 1;
					}
				}
				else
				{
					this.Top -= 5;
				}
			}
			else if (e.KeyCode == Keys.Down)
			{
				if (e.Control)
				{
					if (e.Shift)
					{
						this.Height += 1;
					}
					else
					{
						this.Top += 1;
					}
				}
				else
				{
					this.Top += 5;
				}
			}
		}

		private void HandleResize()
		{
			if (this.IsLocked)
			{
				return;
			}

			switch (this.resizeRegion)
			{
				case ResizeRegion.E:
					{
						int diff = Control.MousePosition.X - this.mouseDownPoint.X;
						this.Width = this.mouseDownRect.Width + diff;
						break;
					}
				case ResizeRegion.S:
					{
						int diff = MousePosition.Y - this.mouseDownPoint.Y;
						this.Height = this.mouseDownRect.Height + diff;
						break;
					}
				case ResizeRegion.SE:
					{
						this.Width = this.mouseDownRect.Width + Control.MousePosition.X - this.mouseDownPoint.X;
						this.Height = this.mouseDownRect.Height + Control.MousePosition.Y - this.mouseDownPoint.Y;
						break;
					}
			}
		}

		private void SetResizeCursor(ResizeRegion region)
		{
			switch (region)
			{
				case ResizeRegion.N:
				case ResizeRegion.S:
					this.Cursor = Cursors.SizeNS;
					break;

				case ResizeRegion.E:
				case ResizeRegion.W:
					this.Cursor = Cursors.SizeWE;
					break;

				case ResizeRegion.NW:
				case ResizeRegion.SE:
					this.Cursor = Cursors.SizeNWSE;
					break;

				default:
					this.Cursor = Cursors.SizeNESW;
					break;
			}
		}

		private ResizeRegion GetResizeRegion(Point clientCursorPos)
		{
			if (clientCursorPos.Y <= this.resizeBorderWidth)
			{
				if (clientCursorPos.X <= this.resizeBorderWidth) return ResizeRegion.NW;
				else if (clientCursorPos.X >= Width - this.resizeBorderWidth) return ResizeRegion.NE;
				else return ResizeRegion.N;
			}
			else if (clientCursorPos.Y >= Height - this.resizeBorderWidth)
			{
				if (clientCursorPos.X <= this.resizeBorderWidth) return ResizeRegion.SW;
				else if (clientCursorPos.X >= Width - this.resizeBorderWidth) return ResizeRegion.SE;
				else return ResizeRegion.S;
			}
			else
			{
				if (clientCursorPos.X <= this.resizeBorderWidth) return ResizeRegion.W;
				else return ResizeRegion.E;
			}
		}

#endregion

		#region Paint

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;

			int height = this.Height;
			int width = this.Width;

			if (IsVertical)
			{
				graphics.RotateTransform(90);
				graphics.TranslateTransform(0, -Width + 1);
				height = this.Width;
				width = this.Height;
			}

			DrawRuler(graphics, width, height, this.Font);

			base.OnPaint(e);
		}

		private static void DrawRuler(Graphics g, int formWidth, int formHeight, Font font)
		{
			// Border
			g.DrawRectangle(Pens.Black, 0, 0, formWidth - 1, formHeight - 1);

			// Width
			g.DrawString(formWidth + " pixels", font, Brushes.Black, 10, (formHeight / 2) - (font.Height / 2));

			// Ticks
			for (int i = 0; i < formWidth; i++)
			{
				if (i % 2 == 0)
				{
					int tickHeight;

					if (i % 100 == 0)
					{
						tickHeight = 15;
						DrawTickLabel(g, i.ToString(), i, formHeight, tickHeight, font);
					}
					else if (i % 10 == 0)
					{
						tickHeight = 10;
					}
					else
					{
						tickHeight = 5;
					}

					DrawTick(g, i, formHeight, tickHeight);
				}
			}
		}

		private static void DrawTick(Graphics g, int xPos, int formHeight, int tickHeight)
		{
			// Top
			g.DrawLine(Pens.Black, xPos, 0, xPos, tickHeight);

			// Bottom
			g.DrawLine(Pens.Black, xPos, formHeight, xPos, formHeight - tickHeight);
		}

		private static void DrawTickLabel(Graphics g, string text, int xPos, int formHeight, int height, Font font)
		{
			// Top
			g.DrawString(text, font, Brushes.Black, xPos, height);

			// Bottom
			g.DrawString(text, font, Brushes.Black, xPos, formHeight - height - font.Height);
		}

#endregion
	}
}