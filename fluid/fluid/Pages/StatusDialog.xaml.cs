using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace fluid_general.Pages
{
    /// <summary>
    /// StausDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class StatusDialog : ModernWpf.Controls.ContentDialog
    {
        /// <summary>
        /// ステータスダイアログのインスタンスを初期化します。
        /// </summary>
        /// <remarks>
        /// ステータスダイアログのインスタンスを初期化します。
        /// </remarks>
        private int _eventId;
        public StatusDialog(string eventId)
        {
            InitializeComponent();
            if (int.TryParse(eventId, out int id))
            {
                _eventId = id;
                Loaded += async (s, e) => await CountStatusAsync();
            }
        }

        private async Task CountStatusAsync()
        {
            try
            {
                var service = App.GetDataService();
                var members = await service.GetMembersAsync();
                var logs = await service.GetCheckInLogsAsync(_eventId);

                // 全メンバーから、このイベントに関連する情報を集計する
                // 現状の設計では CheckInLog が存在するものだけが集計対象になる可能性があるため、
                // 将来的にイベントへの名簿割り当てを実装した際に修正が必要。
                
                int totalParticipants = logs.Count;
                int doneParticipants = logs.Count(l => l.Status == "参加済み");
                
                // TODO: メンバー属性（学年、性別）に基づく集計を実装
                // RosterItem で定義しているロジックを参考に、Member の CustomFields から取得する
                
                wholestatus.Text = $"{doneParticipants}人 / {totalParticipants}人";
                // 他の集計項目も必要に応じて実装
            }
            catch (Exception ex)
            {
                MessageBox.Show($"集計中にエラーが発生しました: {ex.Message}");
            }
        }
    }
}
