using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskPet.Services;

namespace DeskPet.Shell;

public partial class ChatWindow : Window
{
    private static ChatWindow? _instance;

    public static void ShowChat()
    {
        if (_instance == null)
        {
            _instance = new ChatWindow();
            _instance.Closed += (_, _) => _instance = null;
        }
        _instance.Show();
        _instance.Activate();
    }

    private readonly List<AIChatService.ChatMessage> _history = new();
    private bool _busy;
    private Border? _typingBubble;

    private const string SystemPrompt =
        "You are a cute desktop pet (a small animal living on the user's desktop). " +
        "Reply warmly, playfully and briefly (1-3 short sentences). " +
        "You can use a few emoji. Keep the tone light and friendly.";

    public ChatWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AddBubble("Hi! I'm your desktop pet 🐾 What's up?", isUser: false);
            InputBox.Focus();
        };
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_busy) return;
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        AddBubble(text, isUser: true);
        _history.Add(new AIChatService.ChatMessage("user", text));
        InputBox.Clear();
        SetBusy(true);
        ShowTyping();

        try
        {
            var messages = new List<AIChatService.ChatMessage> { new("system", SystemPrompt) };
            messages.AddRange(_history);
            var reply = await AIChatService.Instance.SendAsync(messages);
            RemoveTyping();
            AddBubble(reply, isUser: false);
            _history.Add(new AIChatService.ChatMessage("assistant", reply));
            // Show the AI reply as a speech bubble above the pet.
            DeskPet.Shell.PetWindow.ShowSpeechBubble(reply);
        }
        catch (Exception ex)
        {
            RemoveTyping();
            AddBubble("⚠️ " + ex.Message, isUser: false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SendBtn.IsEnabled = !busy;
        SendBtn.Content = busy ? "…" : "Send";
    }

    private void ShowTyping()
    {
        RemoveTyping();
        _typingBubble = CreateBubble("…", isUser: false);
        MsgPanel.Children.Add(_typingBubble);
        ScrollToEnd();
    }

    private void RemoveTyping()
    {
        if (_typingBubble != null)
        {
            MsgPanel.Children.Remove(_typingBubble);
            _typingBubble = null;
        }
    }

    private void AddBubble(string text, bool isUser)
    {
        MsgPanel.Children.Add(CreateBubble(text, isUser));
        ScrollToEnd();
    }

    private static Border CreateBubble(string text, bool isUser)
    {
        var tb = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            FontSize = 13,
        };
        return new Border
        {
            Background = isUser
                ? new SolidColorBrush(Color.FromRgb(0x5C, 0x8A, 0xD6))
                : new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 280,
            Child = tb,
        };
    }

    private void ScrollToEnd()
    {
        MsgScroll.ScrollToEnd();
    }
}
