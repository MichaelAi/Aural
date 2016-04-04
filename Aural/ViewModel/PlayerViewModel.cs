﻿using Aural.Model;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Aural.ViewModel
{
    public class PlayerViewModel : ViewModelBase
    {

        SystemMediaTransportControls systemMediaControls = null;
        private bool cancelInProgress = false;
        private MusicProperties properties;
        CancellationTokenSource cts;

        private StorageFile _nowPlayingFile;
        public StorageFile NowPlayingFile
        {
            get { return _nowPlayingFile; }
            set { Set("NowPlayingFile", ref _nowPlayingFile, value); }
        }



        private double _nowPlayingMaxDuration;
        public double NowPlayingMaxDuration
        {
            get { return _nowPlayingMaxDuration; }
            set { Set("NowPlayingMaxDuration", ref _nowPlayingMaxDuration, value); }
        }

        private TimeSpan _totalTime;
        public TimeSpan TotalTime
        {
            get { return _totalTime; }
            set { Set("TotalTime", ref _totalTime, value); }
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get { return _currentPosition; }
            set { Set("CurrentPosition", ref _currentPosition, value); }
        }

        private MediaElement _mediaElementObject;
        public MediaElement MediaElementObject
        {
            get { return _mediaElementObject; }
            set { Set("MediaElementObject", ref _mediaElementObject, value); }
        }

        private IRandomAccessStream _nowPlayingStream;
        public IRandomAccessStream NowPlayingStream
        {
            get { return _nowPlayingStream; }
            set { Set("NowPlayingStream", ref _nowPlayingStream, value); }
        }

        private ObservableCollection<PlaylistItem> _currentPlaylistItems = new ObservableCollection<PlaylistItem>();
        public ObservableCollection<PlaylistItem> CurrentPlaylistItems
        {
            get { return _currentPlaylistItems; }
            set { Set("CurrentPlaylistItems", ref _currentPlaylistItems, value); }
        }

        private PlaylistItem _nowPlayingItem;
        public PlaylistItem NowPlayingItem
        {
            get { return _nowPlayingItem; }
            set { Set("NowPlayingItem", ref _nowPlayingItem, value); NowPlayingItem_Changed(); }
        }

        private ObservableCollection<PlaylistItem> _displayedPlaylistItems = new ObservableCollection<PlaylistItem>();
        public ObservableCollection<PlaylistItem> DisplayedPlaylistItems
        {
            get { return _displayedPlaylistItems; }
            set { Set("DisplayedPlaylistItems", ref _displayedPlaylistItems, value); }
        }

        public RelayCommand<PlaylistItem> MediaPlayCommand { get; private set; }
        public RelayCommand MediaPauseCommand { get; private set; }
        public RelayCommand MediaStopCommand { get; private set; }
        public RelayCommand MediaPreviousCommand { get; private set; }
        public RelayCommand MediaNextCommand { get; private set; }
        public RelayCommand MediaStopAfterCurrentCommand { get; private set; }

        public PlayerViewModel()
        {
            MediaPlayCommand = new RelayCommand<PlaylistItem>((id) => MediaPlay(id));
            MediaPauseCommand = new RelayCommand(MediaPause);
            MediaStopCommand = new RelayCommand(MediaStop);
            MediaPreviousCommand = new RelayCommand(MediaPrevious);
            MediaNextCommand = new RelayCommand(MediaNext);
            MediaStopAfterCurrentCommand = new RelayCommand(MediaStopAfterCurrent);

            InitializeMediaObject();
            InitializeSystemMediaControls();
            RegisterMessaging();
        }

        private void RegisterMessaging()
        {
            Messenger.Default.Register<NotificationMessage<ObservableCollection<PlaylistItem>>>(this,
                nm =>
                {
                    if (nm.Notification != null)
                    {
                        if ((string)nm.Notification == "DisplayToCurrent")
                        {
                            TransferPlaylist(nm.Content);
                        }
                        if ((string)nm.Notification == "DisplayToDisplay")
                        {
                            DisplayedPlaylistItems = nm.Content;
                        }
                        if ((string)nm.Notification == "PlayFirst")
                        {
                            DisplayedPlaylistItems = nm.Content;
                            MediaPlay(nm.Content.FirstOrDefault());
                        }
                    }
                }
                );
        }

        private void InitializeMediaObject()
        {
            if (MediaElementObject == null)
            {
                MediaElementObject = new MediaElement() { AutoPlay = true, IsLooping = false, AudioCategory = Windows.UI.Xaml.Media.AudioCategory.BackgroundCapableMedia, AreTransportControlsEnabled = true };
            }
            MediaElementObject.TransportControls.IsCompact = true;
            MediaElementObject.TransportControls.IsZoomButtonVisible = false;
            MediaElementObject.TransportControls.IsZoomEnabled = false;
            MediaElementObject.TransportControls.IsPlaybackRateButtonVisible = true;
            MediaElementObject.TransportControls.IsPlaybackRateEnabled = true;
            MediaElementObject.MediaEnded += MediaElement_MediaEnded;
            systemMediaControls = SystemMediaTransportControls.GetForCurrentView();
        }

        //play
        private async void MediaPlay(PlaylistItem item)
        {
            if (item == null)
            {
                MediaElementObject.Play();
            }
            else
            {
               NowPlayingItem = DisplayedPlaylistItems.Where(x => x == item).FirstOrDefault();
            }
            properties = await TryGetProperties(NowPlayingFile);
            MediaElementObject.SeekCompleted += MediaElementObject_SeekCompleted;
        }

        //attempt to get the properties of the current track
        private async Task<MusicProperties> TryGetProperties(StorageFile file)
        {
            MusicProperties mproperties;
            try
            {
                mproperties = await NowPlayingFile.Properties.GetMusicPropertiesAsync();
            }
            catch
            {
                mproperties = null;
            }
            return mproperties;
        }

        //reset the timer on cancel after current operation
        private void MediaElementObject_SeekCompleted(object sender, RoutedEventArgs e)
        {
            if (cancelInProgress)
            {
                MediaStopAfterCurrent();
            }
        }

        //pause
        private void MediaPause()
        {
            MediaElementObject.Pause();
        }

        //stop
        private void MediaStop()
        {
            MediaElementObject.Stop();
            cancelInProgress = false;
        }

        //previous
        private void MediaPrevious()
        {
            int index = CurrentPlaylistItems.IndexOf(NowPlayingItem);
            if (index > CurrentPlaylistItems.Count - 1 && index > -1)
            {
                NowPlayingItem = CurrentPlaylistItems.ElementAt(index - 1);
            }
        }

        //next
        private void MediaNext()
        {
            int index = CurrentPlaylistItems.IndexOf(NowPlayingItem);
            if (index < CurrentPlaylistItems.Count - 1 && index > -1)
            {
                NowPlayingItem = CurrentPlaylistItems.ElementAt(index + 1);
            }
        }

        //play the next item in the playlist after a song was ended
        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaNext();
        }

        //in case the user changes song or manually cancels  the stop, cancel the stop
        private void CancelStopAfterCurrent()
        {
            if (cts != null)
            {
                cts.Cancel();
            }
        }

        private async void NowPlayingItem_Changed()
        {
            if (NowPlayingItem != null)
            {
                CancelStopAfterCurrent();
                cancelInProgress = false;
                // Open the selected file and set it as the MediaElement's source
                NowPlayingFile = NowPlayingItem.PlaylistFile;
                MediaPlaybackType mediaPlaybackType = MediaPlaybackType.Music;

                // Inform the system transport controls of the media information
                if (!(await systemMediaControls.DisplayUpdater.CopyFromFileAsync(mediaPlaybackType, NowPlayingFile)))
                {
                    //  Problem extracting metadata- just clear everything
                    systemMediaControls.DisplayUpdater.ClearAll();
                }
                systemMediaControls.DisplayUpdater.Update();
                NowPlayingStream = await NowPlayingFile.OpenAsync(FileAccessMode.Read);
                MediaStop();
                MediaElementObject.SetSource(NowPlayingStream, NowPlayingFile.FileType);
                MediaPlay(null);
                GetAlbumArt();
            }
        }

        //Get album art
        //TODO: get album art from files in the folder
        private async void GetAlbumArt()
        {
            using (StorageItemThumbnail thumbnail = await NowPlayingItem.PlaylistFile.GetThumbnailAsync(ThumbnailMode.MusicView, 60))
            {
                if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(thumbnail);
                    if (bitmapImage.PixelHeight != 0)
                    {
                        NowPlayingItem.AlbumArt.Source = bitmapImage;
                    }
                }
                else
                {
                    Debug.WriteLine("Could not open thumbnail");
                }
            }
        }

        private void TransferPlaylist(ObservableCollection<PlaylistItem> DisplayedPlaylistItems)
        {
            if (DisplayedPlaylistItems != null && DisplayedPlaylistItems.Count > 0)
            {
                CurrentPlaylistItems = new ObservableCollection<PlaylistItem>(DisplayedPlaylistItems);
            }
        }

        //Set a timer to stop the playback after this track has ended.
        //the timer changes on seek
        private async void MediaStopAfterCurrent()
        {
            try
            {
                if (cancelInProgress == true)
                    CancelStopAfterCurrent();
                else
                    cancelInProgress = true;
                cts = new CancellationTokenSource();
                Debug.WriteLine(properties.Duration - MediaElementObject.Position);
                await Task.Delay(properties.Duration - MediaElementObject.Position - TimeSpan.FromMilliseconds(100), cts.Token);
                MediaStop();
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Stop canceled");
            }

        }





        //link the player to the system media controls
        private void InitializeSystemMediaControls()
        {
            systemMediaControls.ButtonPressed += SystemMediaControls_ButtonPressed;
            systemMediaControls.IsPlayEnabled = true;
            systemMediaControls.IsPauseEnabled = true;
            systemMediaControls.IsStopEnabled = true;
            systemMediaControls.IsNextEnabled = true;
            systemMediaControls.IsPreviousEnabled = true;
        }

        //handle button presses of the system media controls
        private async void SystemMediaControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs e)
        {
            switch (e.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (MediaElementObject.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Paused || MediaElementObject.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Stopped)
                        {
                            MediaPlay(null);
                        }
                        else
                        {
                            MediaPause();
                        }
                    });
                    break;

                case SystemMediaTransportControlsButton.Pause:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (MediaElementObject.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Paused || MediaElementObject.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Stopped)
                        {
                            MediaPlay(null);
                        }
                        else
                        {
                            MediaPause();
                        }
                    });
                    break;

                case SystemMediaTransportControlsButton.Stop:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        MediaStop();
                    });
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        MediaPrevious();
                    });
                    break;
                case SystemMediaTransportControlsButton.Next:
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        MediaNext();
                    });
                    break;
                default:
                    break;
            }
        }
    }
}
