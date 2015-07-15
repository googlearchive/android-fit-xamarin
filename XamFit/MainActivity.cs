//
//  Copyright 2015  Google Inc. All Rights Reserved.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;

using Android.Support.V7.App;

using Android.Gms.Maps;
using Android.Gms.Fitness;
using Android.Gms.Fitness.Request;
using Android.Gms.Fitness.Result;
using Android.Gms.Fitness.Data;
using Android.Gms.Common.Apis;
using Android.Gms.Common;

using TimeUnit = Java.Util.Concurrent.TimeUnit;

namespace Google.XamarinSamples.XamFit
{
	/*
	 * Implements activity and Google Fit permissions request
	 */
	[Activity (Theme = "@style/Theme.XamFitTheme",
		Label = "XamFit", MainLauncher = true, Icon = "@drawable/icon")]
	public partial class MainActivity : AppCompatActivity
	{
		const string Tag = "MainActivity";

		bool authInProgress = false;
		IGoogleApiClient googleApiClient;
		const int OAUTH_REQUEST_CODE = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.Main);

			// extract authInProgress from bundle if it's in there
			if (bundle != null && bundle.ContainsKey ("authInProgress")) {
				authInProgress = bundle.GetBoolean ("authInProgress");
			}

			BuildApiClient ();
		}

		/**
		 * Attempt to connect to Google Play Services on activity start
		 */
		protected override void OnStart ()
		{
			base.OnStart ();
			googleApiClient.Connect ();
		}

		/**
		 * Disconnect from Google Play Services when the activity stops
		 */
		protected override void OnStop ()
		{
			if (googleApiClient.IsConnected) {
				googleApiClient.Disconnect ();
			}
			base.OnStop ();
		}

		protected override void OnActivityResult(int requestCode, Android.App.Result resultCode, Intent data) {
			if (Log.IsLoggable (Tag, LogPriority.Info)) {
				Log.Info (Tag, "In onActivityResult");
			}
			if (requestCode == OAUTH_REQUEST_CODE) {
				authInProgress = false;
				if (resultCode == Android.App.Result.Ok) {
					// Make sure the app is not already connected or attempting to connect
					if (!googleApiClient.IsConnecting && !googleApiClient.IsConnected) {
						googleApiClient.Connect();
					}
				}
			}

		}

		protected override void OnSaveInstanceState(Bundle bundle) {
			bundle.PutBoolean ("authInProgress", authInProgress);
			base.OnSaveInstanceState(bundle);
		}

		/**
		 * Build the Google Play Services API client
		 */
		private void BuildApiClient ()
		{
			// Create and connect the Google API client
			googleApiClient = new GoogleApiClientBuilder (this)
				.AddApi (FitnessClass.HISTORY_API)
				.AddScope (FitnessClass.ScopeActivityRead)
				.AddConnectionCallbacks (
					// connection succeeded
					connectionHint => {
						if (Log.IsLoggable (Tag, LogPriority.Info)) {
							Log.Info (Tag, "Connected to the Google API client");
						}
						// Get step data from Google Play Services
						readSteps ();
					},
					// connection suspended
					cause => {
						if (Log.IsLoggable (Tag, LogPriority.Info)) {
							Log.Info (Tag, "Connection suspended");
						}
					}
				)
				.AddOnConnectionFailedListener (
					// connection failed
					result => {
						if (Log.IsLoggable (Tag, LogPriority.Info)) {
							Log.Info (Tag, "Failed to connect to the Google API client");
						}
						if (!result.HasResolution) {
							GoogleApiAvailability.Instance.GetErrorDialog (this, result.ErrorCode, 0).Show ();
							return;
						}

						if (!authInProgress) {
							try
							{
								if (Log.IsLoggable (Tag, LogPriority.Info)) {
									Log.Info (Tag, "Attempting to resolve failed connection");
								}
								authInProgress = true;
								result.StartResolutionForResult(this, OAUTH_REQUEST_CODE);
							}
							catch (IntentSender.SendIntentException e) {
								if (Log.IsLoggable (Tag, LogPriority.Error)) {
									Log.Error (Tag, "Exception while starting resolution activity", e);
									authInProgress = false;
								}
							}
						}
					}
				)
				.Build ();
		}

	}

	/*
	 * Implements the Google Fit data access
	 */
	public partial class MainActivity: IResultCallback
	{
		private void readSteps ()
		{
			DataReadRequest readRequest = new DataReadRequest.Builder ()
				.Aggregate (Android.Gms.Fitness.Data.DataType.TypeStepCountDelta, Android.Gms.Fitness.Data.DataType.AggregateStepCountDelta)
				.BucketByTime (1, TimeUnit.Days)
				.SetTimeRange (TimeUtility.TwoDaysAgoMillis (), TimeUtility.CurrentMillis(), TimeUnit.Milliseconds)
				.Build();

			FitnessClass.HistoryApi.ReadData (googleApiClient, readRequest).SetResultCallback (this);
		}

		public void OnResult (Java.Lang.Object obj)
		{
			IList<Bucket> buckets = ((DataReadResult)obj).Buckets;
			// There should be at least two buckets; the last being the latest 24 hour period
			if (buckets.Count < 2) {
				if (Log.IsLoggable (Tag, LogPriority.Error))
				{
					Log.Error (Tag, $"Too few buckets returned: {buckets.Count}");
				}
				return;
			}

			// last bucket is previous 24 hours
			int last24Hours = ExtractStepValue (buckets[buckets.Count - 1]);
			// second-last bucket's the 24 hours previous to the above
			int last48Hours = ExtractStepValue (buckets [buckets.Count - 2]) + last24Hours;

			(FindViewById<TextView> (Resource.Id.past24Value)).Text = last24Hours.ToString ();
			(FindViewById<TextView> (Resource.Id.past48Value)).Text = last48Hours.ToString ();
		}

		private int ExtractStepValue (Bucket bucket)
		{
			// There should only be 1 data set and 0 or 1 data points, but for
			// clarity we'll loop through to show the structures
			int steps = 0;
			foreach (DataSet ds in bucket.DataSets) {
				foreach (DataPoint p in ds.DataPoints) {
					foreach (Field f in p.DataType.Fields) {
						steps += p.GetValue (f).AsInt();
					}
				}
			}
			return steps;
		}
	}
}

