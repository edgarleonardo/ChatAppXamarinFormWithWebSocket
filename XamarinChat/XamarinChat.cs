using System;

using Xamarin.Forms;

namespace XamarinChat
{
	public class App : Application
	{
        public static string SignalRUrl { get { return "http://192.168.0.8:62265/"; } }
		public App ()
		{
			// The root page of your application
			MainPage = new ChatPage{BindingContext = new ChatViewModel()};
		}

		protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}

