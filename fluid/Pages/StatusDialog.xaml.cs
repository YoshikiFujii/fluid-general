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
        string eventFilePath;
        public StatusDialog(string EventFilePath)
        {
            InitializeComponent();
            eventFilePath = EventFilePath;
            CountStatus();
        }
        private void CountStatus()
        {
            XDocument eventDoc = XDocument.Load(eventFilePath);
            int TotalParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" |
                                                 (string)e.Element("Status") == "未参加");
            int FirstTotalParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Year") == "新");
            int SecondTotalParticipants = TotalParticipants - FirstTotalParticipants;
            int maleTotalParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Gender") == "男");
            int femaleTotalParticipants = TotalParticipants - maleTotalParticipants;

            int DoneParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Status") == "参加済み");
            int FirstParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" &&
                                                 (string)e.Element("Year") == "新");
            int SecondParticipants = DoneParticipants - FirstParticipants;
            int maleParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Status") == "参加済み" &&
                                                                            (string)e.Element("Gender") == "男");
            int femaleParticipants = DoneParticipants - maleParticipants;
            wholestatus.Text = $"{DoneParticipants}人 / {TotalParticipants}人";
            firststatus.Text = $"{FirstParticipants}人 / {FirstTotalParticipants}人";
            secondstatus.Text = $"{SecondParticipants}人 / {SecondTotalParticipants} 人";
            malestatus.Text = $"{maleParticipants}人 / {maleTotalParticipants} 人";
            femalestatus.Text = $"{femaleParticipants}人 / {femaleTotalParticipants} 人";

        }
    }
}
