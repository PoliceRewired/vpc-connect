﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ConnectApp.AppLog;
using ConnectApp.Entities;
using ConnectApp.Extensions;
using ConnectApp.Pages.Lists;
using ConnectApp.Pages.Models;
using Newtonsoft.Json;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ConnectApp.Pages
{
    public abstract class BaseAppContentPage<M> : ContentPage
        where M : AbstractBaseModel
    {
        public static readonly int MAX_NOTIFICATIONS_RECENT = 5;
        public static readonly int LOGO_CLICK_THRESHOLD = 20;

        public App app;
        public AppLogger log;
        protected int logoClickCount = 0;
        protected UriHelper<BaseAppContentPage<M>, M> uris;

        public M Model { get; private set; }

        protected BaseAppContentPage(App app = null)
        {
            this.app = app;
            this.log = app.Log.For(this);
            this.uris = new UriHelper<BaseAppContentPage<M>, M>(this);
            this.Model = InitModel(app);
            BindingContext = Model;
        }

        protected abstract M InitModel(App app);

        protected override void OnAppearing()
        {
            log.Debug("BaseAppContentPage.OnAppearing", false);
            base.OnAppearing();

            // retrieve the 3 most recent notifications
            var recentNotifications = app.Db.GetNotificationRecords(MAX_NOTIFICATIONS_RECENT);
            Model.RecentNotifications =
                new ObservableCollection<NotificationListItem>(
                    recentNotifications.Select(n => new NotificationListItem(n)));

            // subscribe to model events
            Model.OnUriRequested += Model_OnUriRequested;

            // subscribe to app events
            app.OnAppActivity += App_OnStateChange;
            app.OnAppRegistrationStateChange += App_OnAppRegistrationStateChange;
            app.OnNotificationReceived += App_OnNotificationReceived_UpdateRecentNotifications;
            app.OnNotificationsErased += App_OnNotificationsErased_UpdateRecentNotifications;
            app.OnNotificationDeleted += App_OnNotificationDeleted_UpdateRecentNotifications;

            // initiate an update based on the last known app state
            this.App_OnStateChange(app.LastActivity);
        }

        protected override void OnDisappearing()
        {
            log.Debug("BaseAppContentPage.OnDisappearing", false);
            base.OnDisappearing();

            // unsubscribe from model events
            Model.OnUriRequested -= Model_OnUriRequested;

            // unsubscribe from app events
            app.OnAppActivity -= App_OnStateChange;
            app.OnAppRegistrationStateChange -= App_OnAppRegistrationStateChange;
            app.OnNotificationReceived -= App_OnNotificationReceived_UpdateRecentNotifications;
            app.OnNotificationsErased -= App_OnNotificationsErased_UpdateRecentNotifications;
            app.OnNotificationDeleted -= App_OnNotificationDeleted_UpdateRecentNotifications;
        }

        private void App_OnAppRegistrationStateChange(App.RegistrationStates state)
        {
            OnRegistrationStateChanged(state);
        }

        private void App_OnStateChange(App.AppActivity state)
        {
            Model.AppState = state;
            OnAppActivity(state);
        }

        protected abstract void OnAppActivity(App.AppActivity state);

        protected abstract void OnRegistrationStateChanged(App.RegistrationStates state);

        private void App_OnNotificationReceived_UpdateRecentNotifications(NotificationRecord notification)
        {
            log.Debug("BaseAppContentPage.App_OnNotificationReceived_UpdateRecentNotifications", false);
            Device.BeginInvokeOnMainThread(() =>
            {
                // only perform the insert if there isn't already a matching notification
                var existing = Model.RecentNotifications.SingleOrDefault(n => n.Record.NotificationId == notification.NotificationId);

                if (existing == null)
                {
                    // remove last item, if there are already MAXIMUM
                    if (Model.RecentNotifications.Count >= MAX_NOTIFICATIONS_RECENT)
                    {
                        Model.RecentNotifications.RemoveAt(Model.RecentNotifications.Count - 1);
                    }
                    // add the new item to the start of the list
                    Model.RecentNotifications.Insert(0, new NotificationListItem(notification));
                }
                else
                {
                    // update the record in the existing found item
                    existing.Record = notification;
                }
            });
        }

        private void App_OnNotificationsErased_UpdateRecentNotifications()
        {
            log.Debug("BaseAppContentPage.App_OnNotificationsErased_UpdateRecentNotifications", false);
            Device.BeginInvokeOnMainThread(() =>
            {
                Model.RecentNotifications.Clear();
            });
        }

        private void App_OnNotificationDeleted_UpdateRecentNotifications(NotificationRecord record)
        {
            log.Debug("BaseAppContentPage.App_OnNotificationDeleted_UpdateRecentNotifications", false);
            var found = Model.RecentNotifications.SingleOrDefault(n => n.Record.NotificationId == record.NotificationId);
            if (found != null)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Model.RecentNotifications.Remove(found);
                });
            }
        }

        protected void Btn_LogoClicked(object sender, EventArgs args)
        {
            log.Debug("BaseAppContentPage.Btn_LogoClicked", false);
            logoClickCount++;
            if (logoClickCount >= LOGO_CLICK_THRESHOLD)
            {
                app.SwitchToPage(PageTypes.DebugPage);
            }
        }

        protected void SeeAll_Tapped(object sender, EventArgs args)
        {
            log.Debug("BaseAppContentPage.SeeAll_Tapped", false);
            app.SwitchToPage(PageTypes.NotificationsPage);
        }

        protected async Task ListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            log.Debug("BaseAppContentPage.ListView_ItemTapped", false);
            var item = e.Item as NotificationListItem;
            if (item != null)
            {
                log.Verbose($"Target url: " + item.Record?.TargetUrl, false);
                var uri = PortalHelper.PreparePortalAnalyticsUriQuery(item.Uri, PortalHelper.AnalyticsAction.OpenNotification, this, log);
                await uris.OpenLinkAsync(uri);
            }
            else
            {
                log.Debug("List item was null.", false);
            }
        }

        private async void Model_OnUriRequested(string uri)
        {
            await uris.OpenLinkAsync(uri);
        }


    }
}
