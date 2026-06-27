using System;
using System.Drawing;
using System.Windows.Forms;

namespace MapEditorTool.UI
{
    public sealed class DeveloperCommentBox : Form
    {
        private readonly TextBox _commentBox;

        public DeveloperCommentBox(string sourceDescription)
        {
            Text = "DeveloperCommentBox";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 300);

            var sourceLabel = new Label
            {
                AutoSize = false,
                Left = 12,
                Top = 12,
                Width = 596,
                Height = 74,
                Text = "Event source:\r\n" + sourceDescription
            };

            var prompt = new Label
            {
                AutoSize = false,
                Left = 12,
                Top = 94,
                Width = 596,
                Height = 24,
                Text = "Developer comment:"
            };

            _commentBox = new TextBox
            {
                Left = 12,
                Top = 122,
                Width = 596,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };
            _commentBox.KeyDown += CommentBoxKeyDown;

            var ok = new Button
            {
                Text = "确认",
                Left = 426,
                Top = 258,
                Width = 86,
                DialogResult = DialogResult.OK
            };

            var cancel = new Button
            {
                Text = "取消",
                Left = 522,
                Top = 258,
                Width = 86,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(sourceLabel);
            Controls.Add(prompt);
            Controls.Add(_commentBox);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        public string CommentText
        {
            get { return _commentBox.Text; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _commentBox.Focus();
        }

        private void CommentBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Shift)
                return;

            DialogResult = DialogResult.OK;
            e.SuppressKeyPress = true;
            Close();
        }
    }
}
