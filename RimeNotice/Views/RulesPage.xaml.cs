using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RimeNotice.Models;
using RimeNotice.Services;
namespace RimeNotice.Views;
public sealed partial class RulesPage : Page
{
    private bool _ready;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    public RulesPage()
    {
        InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        Loaded += (_, _) => LoadSettings();
        Unloaded += (_, _) => { if (_saveTimer.IsEnabled) { _saveTimer.Stop(); Save(); } };
    }
    private void RestoreDefaults_Click(object sender,RoutedEventArgs e)
    {
        _saveTimer.Stop();_ready=false;
        var defaults=RuleSettings.ForNewUser();Contacts.Text=string.Join("\n",defaults.ImportantContacts);Keywords.Text=string.Join("\n",defaults.ImportantKeywords);
        _ready=true;Save();
    }
    private void LoadSettings()
    {
        _ready = false; var s = SettingsStore.Shared.Current;
        Contacts.Text = string.Join("\n", s.ImportantContacts); Keywords.Text = string.Join("\n", s.ImportantKeywords);
        Blacklist.Text = string.Join("\n", s.Blacklist); Whitelist.Text = string.Join("\n", s.Whitelist);
        Mention.IsOn = s.MentionImportant; Cooldown.Value = s.CooldownSeconds;
        Calling.IsOn = s.CallingEnabled; CallingKeywords.Text = string.Join("\n", s.CallingKeywords);
        _ready = true;
    }
    private void Save()
    {
        if (!_ready || double.IsNaN(Cooldown.Value)) return;
        try
        {
            SettingsStore.Shared.Save(SettingsStore.Shared.Current with {
                ImportantContacts = RuleSettings.Terms(Contacts.Text), ImportantKeywords = RuleSettings.Terms(Keywords.Text),
                Blacklist = RuleSettings.Terms(Blacklist.Text), Whitelist = RuleSettings.Terms(Whitelist.Text),
                MentionImportant = Mention.IsOn, CooldownSeconds = (int)Cooldown.Value,
                CallingEnabled = Calling.IsOn, CallingKeywords = RuleSettings.Terms(CallingKeywords.Text)
            });
            SaveError.IsOpen = false;
        }
        catch (Exception ex) { SaveError.Message = "无法写入设置，请检查文件访问权限；更改尚未生效。"; SaveError.IsOpen = true; QQNotificationListener.Shared.ReportPresentationError(ex); }
    }
    private void RuleChanged(object sender, RoutedEventArgs e) => Save();
    private void RuleTextChanged(object sender, TextChangedEventArgs e) { if (!_ready) return; _saveTimer.Stop(); _saveTimer.Start(); }
    private void RuleNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs e) => Save();
}
