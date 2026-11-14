using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProxiFyreUI.Forms
{
    internal sealed class BodexActivationForm : Form
    {
        private readonly TextBox _code = new TextBox();
        private readonly Button _activate = new Button();
        private readonly Label _status = new Label();

        internal BodexActivationForm()
        {
            Text = "BODEX Proxy — تفعيل الاشتراك";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 260);
            BackColor = Color.FromArgb(11, 18, 32);
            ForeColor = Color.FromArgb(226, 238, 250);
            Font = new Font("Segoe UI", 10F);

            var title = new Label
            {
                Text = "BODEX Proxy",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 200, 245),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 62
            };
            var prompt = new Label
            {
                Text = "أدخل كود الاشتراك لتفعيل البرنامج",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 34
            };
            _code.SetBounds(35, 112, 360, 34);
            _code.Font = new Font("Segoe UI", 12F);
            _code.TextAlign = HorizontalAlignment.Center;
            _code.CharacterCasing = CharacterCasing.Upper;
            _code.BackColor = Color.FromArgb(7, 14, 25);
            _code.ForeColor = Color.White;
            _code.BorderStyle = BorderStyle.FixedSingle;

            _activate.Text = "تفعيل BODEX Proxy";
            _activate.SetBounds(35, 158, 360, 38);
            _activate.FlatStyle = FlatStyle.Flat;
            _activate.FlatAppearance.BorderSize = 0;
            _activate.BackColor = Color.FromArgb(32, 200, 245);
            _activate.ForeColor = Color.FromArgb(5, 16, 28);
            _activate.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _activate.Click += ActivateClicked;

            _status.SetBounds(25, 204, 380, 42);
            _status.TextAlign = ContentAlignment.MiddleCenter;
            _status.ForeColor = Color.FromArgb(159, 177, 200);
            _status.Text = "يتم التحقق من الاشتراك عند تشغيل البرنامج.";

            Controls.Add(title);
            Controls.Add(prompt);
            Controls.Add(_code);
            Controls.Add(_activate);
            Controls.Add(_status);
            AcceptButton = _activate;
        }

        private async void ActivateClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_code.Text))
            {
                _status.Text = "أدخل كود الاشتراك أولاً.";
                return;
            }

            _activate.Enabled = false;
            _code.Enabled = false;
            _status.Text = "جارٍ الاتصال بخادم التفعيل...";
            try
            {
                var expiry = await ProxiFyreUI.Infrastructure.BodexLicense.ActivateAsync(_code.Text);
                _status.Text = "تم التفعيل بنجاح — ينتهي: " + expiry;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
                _activate.Enabled = true;
                _code.Enabled = true;
                _code.Focus();
            }
        }
    }
}
