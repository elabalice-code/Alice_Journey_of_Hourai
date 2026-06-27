namespace MapEditorTool.UI
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.MenuStrip mainMenu;
        private System.Windows.Forms.SplitContainer rootSplit;
        private System.Windows.Forms.ListBox mapsList;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage mapTab;
        private System.Windows.Forms.TabPage linksTab;
        private System.Windows.Forms.SplitContainer mapTabSplit;
        private System.Windows.Forms.Panel mapCanvasHost;
        private System.Windows.Forms.ToolStrip mapTools;
        private System.Windows.Forms.Label mapPlaceholder;
        private System.Windows.Forms.Panel mapPropertyPanel;
        private System.Windows.Forms.PropertyGrid mapPropertyGrid;
        private System.Windows.Forms.Panel developerCommentPanel;
        private System.Windows.Forms.CheckBox developerCommentModeCheckBox;
        private System.Windows.Forms.SplitContainer linksTabSplit;
        private System.Windows.Forms.Label linksPlaceholder;
        private System.Windows.Forms.SplitContainer linksSplit;
        private System.Windows.Forms.ListBox linksList;
        private System.Windows.Forms.PropertyGrid linkPropertyGrid;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusText;
        private System.Windows.Forms.ContextMenuStrip mapListContextMenu;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mainMenu = new System.Windows.Forms.MenuStrip();
            this.rootSplit = new System.Windows.Forms.SplitContainer();
            this.mapsList = new System.Windows.Forms.ListBox();
            this.tabs = new System.Windows.Forms.TabControl();
            this.mapTab = new System.Windows.Forms.TabPage();
            this.linksTab = new System.Windows.Forms.TabPage();
            this.mapTabSplit = new System.Windows.Forms.SplitContainer();
            this.mapCanvasHost = new System.Windows.Forms.Panel();
            this.mapTools = new System.Windows.Forms.ToolStrip();
            this.mapPlaceholder = new System.Windows.Forms.Label();
            this.mapPropertyPanel = new System.Windows.Forms.Panel();
            this.mapPropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.developerCommentPanel = new System.Windows.Forms.Panel();
            this.developerCommentModeCheckBox = new System.Windows.Forms.CheckBox();
            this.linksTabSplit = new System.Windows.Forms.SplitContainer();
            this.linksPlaceholder = new System.Windows.Forms.Label();
            this.linksSplit = new System.Windows.Forms.SplitContainer();
            this.linksList = new System.Windows.Forms.ListBox();
            this.linkPropertyGrid = new System.Windows.Forms.PropertyGrid();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusText = new System.Windows.Forms.ToolStripStatusLabel();
            this.mapListContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.rootSplit)).BeginInit();
            this.rootSplit.Panel1.SuspendLayout();
            this.rootSplit.Panel2.SuspendLayout();
            this.rootSplit.SuspendLayout();
            this.tabs.SuspendLayout();
            this.mapTab.SuspendLayout();
            this.linksTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mapTabSplit)).BeginInit();
            this.mapTabSplit.Panel1.SuspendLayout();
            this.mapTabSplit.Panel2.SuspendLayout();
            this.mapTabSplit.SuspendLayout();
            this.mapCanvasHost.SuspendLayout();
            this.mapPropertyPanel.SuspendLayout();
            this.developerCommentPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.linksTabSplit)).BeginInit();
            this.linksTabSplit.Panel1.SuspendLayout();
            this.linksTabSplit.Panel2.SuspendLayout();
            this.linksTabSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.linksSplit)).BeginInit();
            this.linksSplit.Panel1.SuspendLayout();
            this.linksSplit.Panel2.SuspendLayout();
            this.linksSplit.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mainMenu.Location = new System.Drawing.Point(0, 0);
            this.mainMenu.Name = "mainMenu";
            this.mainMenu.Size = new System.Drawing.Size(1182, 24);
            this.mainMenu.TabIndex = 0;
            // 
            // rootSplit
            // 
            this.rootSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.rootSplit.Location = new System.Drawing.Point(0, 24);
            this.rootSplit.Name = "rootSplit";
            // 
            // rootSplit.Panel1
            // 
            this.rootSplit.Panel1.Controls.Add(this.mapsList);
            this.rootSplit.Panel1MinSize = 200;
            // 
            // rootSplit.Panel2
            // 
            this.rootSplit.Panel2.Controls.Add(this.tabs);
            this.rootSplit.Size = new System.Drawing.Size(1182, 707);
            this.rootSplit.SplitterDistance = 220;
            this.rootSplit.TabIndex = 1;
            // 
            // mapsList
            // 
            this.mapsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapsList.FormattingEnabled = true;
            this.mapsList.HorizontalScrollbar = true;
            this.mapsList.ItemHeight = 15;
            this.mapsList.Name = "mapsList";
            this.mapsList.Size = new System.Drawing.Size(220, 707);
            this.mapsList.TabIndex = 0;
            // 
            // tabs
            // 
            this.tabs.Controls.Add(this.mapTab);
            this.tabs.Controls.Add(this.linksTab);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(958, 707);
            this.tabs.TabIndex = 0;
            // 
            // mapTab
            // 
            this.mapTab.Controls.Add(this.mapTabSplit);
            this.mapTab.Location = new System.Drawing.Point(4, 25);
            this.mapTab.Name = "mapTab";
            this.mapTab.Padding = new System.Windows.Forms.Padding(3);
            this.mapTab.Size = new System.Drawing.Size(950, 678);
            this.mapTab.TabIndex = 0;
            this.mapTab.Text = "地图";
            this.mapTab.UseVisualStyleBackColor = true;
            // 
            // linksTab
            // 
            this.linksTab.Controls.Add(this.linksTabSplit);
            this.linksTab.Location = new System.Drawing.Point(4, 25);
            this.linksTab.Name = "linksTab";
            this.linksTab.Padding = new System.Windows.Forms.Padding(3);
            this.linksTab.Size = new System.Drawing.Size(950, 678);
            this.linksTab.TabIndex = 1;
            this.linksTab.Text = "连接";
            this.linksTab.UseVisualStyleBackColor = true;
            // 
            // mapTabSplit
            // 
            this.mapTabSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapTabSplit.Location = new System.Drawing.Point(3, 3);
            this.mapTabSplit.Name = "mapTabSplit";
            // 
            // mapTabSplit.Panel1
            // 
            this.mapTabSplit.Panel1.Controls.Add(this.mapCanvasHost);
            this.mapTabSplit.Panel1MinSize = 460;
            // 
            // mapTabSplit.Panel2
            // 
            this.mapTabSplit.Panel2.Controls.Add(this.mapPropertyPanel);
            this.mapTabSplit.Size = new System.Drawing.Size(944, 672);
            this.mapTabSplit.SplitterDistance = 650;
            this.mapTabSplit.TabIndex = 0;
            // 
            // mapCanvasHost
            // 
            this.mapCanvasHost.Controls.Add(this.mapPlaceholder);
            this.mapCanvasHost.Controls.Add(this.mapTools);
            this.mapCanvasHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapCanvasHost.Name = "mapCanvasHost";
            this.mapCanvasHost.Size = new System.Drawing.Size(650, 672);
            this.mapCanvasHost.TabIndex = 0;
            // 
            // mapTools
            // 
            this.mapTools.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.mapTools.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mapTools.Location = new System.Drawing.Point(0, 0);
            this.mapTools.Name = "mapTools";
            this.mapTools.Size = new System.Drawing.Size(650, 25);
            this.mapTools.TabIndex = 0;
            // 
            // mapPlaceholder
            // 
            this.mapPlaceholder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.mapPlaceholder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.mapPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapPlaceholder.Location = new System.Drawing.Point(0, 25);
            this.mapPlaceholder.Name = "mapPlaceholder";
            this.mapPlaceholder.Size = new System.Drawing.Size(650, 647);
            this.mapPlaceholder.TabIndex = 1;
            this.mapPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // mapPropertyPanel
            // 
            this.mapPropertyPanel.Controls.Add(this.mapPropertyGrid);
            this.mapPropertyPanel.Controls.Add(this.developerCommentPanel);
            this.mapPropertyPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapPropertyPanel.Name = "mapPropertyPanel";
            this.mapPropertyPanel.Size = new System.Drawing.Size(290, 672);
            this.mapPropertyPanel.TabIndex = 0;
            // 
            // mapPropertyGrid
            // 
            this.mapPropertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mapPropertyGrid.HelpVisible = true;
            this.mapPropertyGrid.Name = "mapPropertyGrid";
            this.mapPropertyGrid.Size = new System.Drawing.Size(290, 620);
            this.mapPropertyGrid.TabIndex = 0;
            this.mapPropertyGrid.ToolbarVisible = false;
            // 
            // developerCommentPanel
            // 
            this.developerCommentPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.developerCommentPanel.Controls.Add(this.developerCommentModeCheckBox);
            this.developerCommentPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.developerCommentPanel.Name = "developerCommentPanel";
            this.developerCommentPanel.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.developerCommentPanel.Size = new System.Drawing.Size(290, 52);
            this.developerCommentPanel.TabIndex = 1;
            // 
            // developerCommentModeCheckBox
            // 
            this.developerCommentModeCheckBox.AutoSize = true;
            this.developerCommentModeCheckBox.Dock = System.Windows.Forms.DockStyle.Left;
            this.developerCommentModeCheckBox.Name = "developerCommentModeCheckBox";
            this.developerCommentModeCheckBox.Size = new System.Drawing.Size(174, 36);
            this.developerCommentModeCheckBox.TabIndex = 0;
            this.developerCommentModeCheckBox.Text = "Developer Comment Mode";
            this.developerCommentModeCheckBox.UseVisualStyleBackColor = true;
            // 
            // linksTabSplit
            // 
            this.linksTabSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linksTabSplit.Location = new System.Drawing.Point(3, 3);
            this.linksTabSplit.Name = "linksTabSplit";
            // 
            // linksTabSplit.Panel1
            // 
            this.linksTabSplit.Panel1.Controls.Add(this.linksPlaceholder);
            this.linksTabSplit.Panel1MinSize = 360;
            // 
            // linksTabSplit.Panel2
            // 
            this.linksTabSplit.Panel2.Controls.Add(this.linksSplit);
            this.linksTabSplit.Size = new System.Drawing.Size(944, 672);
            this.linksTabSplit.SplitterDistance = 520;
            this.linksTabSplit.TabIndex = 0;
            // 
            // linksPlaceholder
            // 
            this.linksPlaceholder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.linksPlaceholder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.linksPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linksPlaceholder.Name = "linksPlaceholder";
            this.linksPlaceholder.Size = new System.Drawing.Size(520, 672);
            this.linksPlaceholder.TabIndex = 0;
            this.linksPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // linksSplit
            // 
            this.linksSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linksSplit.Location = new System.Drawing.Point(0, 0);
            this.linksSplit.Name = "linksSplit";
            // 
            // linksSplit.Panel1
            // 
            this.linksSplit.Panel1.Controls.Add(this.linksList);
            this.linksSplit.Panel1MinSize = 180;
            // 
            // linksSplit.Panel2
            // 
            this.linksSplit.Panel2.Controls.Add(this.linkPropertyGrid);
            this.linksSplit.Size = new System.Drawing.Size(420, 672);
            this.linksSplit.SplitterDistance = 200;
            this.linksSplit.TabIndex = 0;
            // 
            // linksList
            // 
            this.linksList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linksList.FormattingEnabled = true;
            this.linksList.HorizontalScrollbar = true;
            this.linksList.ItemHeight = 15;
            this.linksList.Name = "linksList";
            this.linksList.Size = new System.Drawing.Size(200, 672);
            this.linksList.TabIndex = 0;
            // 
            // linkPropertyGrid
            // 
            this.linkPropertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linkPropertyGrid.HelpVisible = true;
            this.linkPropertyGrid.Name = "linkPropertyGrid";
            this.linkPropertyGrid.Size = new System.Drawing.Size(216, 672);
            this.linkPropertyGrid.TabIndex = 0;
            this.linkPropertyGrid.ToolbarVisible = false;
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusText});
            this.statusStrip.Location = new System.Drawing.Point(0, 731);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1182, 22);
            this.statusStrip.TabIndex = 2;
            // 
            // statusText
            // 
            this.statusText.Name = "statusText";
            this.statusText.Size = new System.Drawing.Size(1167, 17);
            this.statusText.Spring = true;
            this.statusText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // mapListContextMenu
            // 
            this.mapListContextMenu.Name = "mapListContextMenu";
            this.mapListContextMenu.Size = new System.Drawing.Size(61, 4);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1182, 753);
            this.Controls.Add(this.rootSplit);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainMenu);
            this.MainMenuStrip = this.mainMenu;
            this.Name = "Form1";
            this.Text = "MapEditorTool";
            this.rootSplit.Panel1.ResumeLayout(false);
            this.rootSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rootSplit)).EndInit();
            this.rootSplit.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.mapTab.ResumeLayout(false);
            this.linksTab.ResumeLayout(false);
            this.mapTabSplit.Panel1.ResumeLayout(false);
            this.mapTabSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mapTabSplit)).EndInit();
            this.mapTabSplit.ResumeLayout(false);
            this.mapCanvasHost.ResumeLayout(false);
            this.mapCanvasHost.PerformLayout();
            this.mapPropertyPanel.ResumeLayout(false);
            this.developerCommentPanel.ResumeLayout(false);
            this.developerCommentPanel.PerformLayout();
            this.linksTabSplit.Panel1.ResumeLayout(false);
            this.linksTabSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.linksTabSplit)).EndInit();
            this.linksTabSplit.ResumeLayout(false);
            this.linksSplit.Panel1.ResumeLayout(false);
            this.linksSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.linksSplit)).EndInit();
            this.linksSplit.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
