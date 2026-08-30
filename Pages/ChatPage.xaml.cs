namespace FairyAI_Android.Pages;

public partial class ChatPage : ContentPage
{
    public ChatPage(ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
