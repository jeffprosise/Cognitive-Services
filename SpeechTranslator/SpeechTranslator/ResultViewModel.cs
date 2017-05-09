// ----------------------------------------------------------------------
// <copyright file="ResultViewModel.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.
// All rights reserved.
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
// </copyright>
// ----------------------------------------------------------------------
// <summary>ResultViewModel.cs</summary>
// ----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SpeechTranslator
{
    /// <summary>
    /// Output Result Model that contains the Recognition and Translation result.
    /// </summary>
    public class Result
    {

        /// <summary>
        /// Recognition Result
        /// </summary>
        public string Recognition { get; set; }

        /// <summary>
        /// Translation Result
        /// </summary>
        public string Translation { get; set; }

        /// <summary>
        /// Status Message to be displayed in case of error
        /// </summary>
        public string Status { get; set; }
    }

    /// <summary>
    /// ViewModel that connects to the ListView
    /// </summary>
    public class ResultViewModel
    {
        private ObservableCollection<Result> results = new ObservableCollection<Result>();
        private CoreDispatcher dispatcher;

        /// <summary>
        /// List of Results that will populate the UI List View
        /// </summary>
        public ObservableCollection<Result> Results { get { return results; } }
        
        public ResultViewModel()
        {
            dispatcher = Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher;
        }

        /// <summary>
        /// Clear the result list
        /// </summary>
        /// <returns></returns>
        public async Task Clear()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => results.Clear());
        }

        /// <summary>
        /// Add to the result list
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public async Task Add(Result result)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => results.Add(result));
        }
    }
}
