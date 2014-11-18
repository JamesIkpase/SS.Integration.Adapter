﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Interface
{
    public interface IStreamListenerManager
    {
        bool HasStreamListener(string fixtureId);
        void StartStreaming(string fixtureId);
        void StopStreaming(string fixtureId);

        event Adapter.StreamEventHandler StreamCreated;
        event Adapter.StreamEventHandler StreamRemoved;
        
        int ListenersCount { get; }
        void StopAll();
        
        /// <summary>
        /// This method indicates which fixtures are currently present in the feed
        /// for a given sport
        /// </summary>
        void UpdateCurrentlyAvailableFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup);
        void CreateStreamListener(IResourceFacade resource, IStateManager stateManager, IAdapterPlugin platformConnector);
        bool RemoveStreamListener(string fixtureId);

        IEnumerable<IGrouping<string, IListener>> GetListenersBySport();
        bool WillProcessResource(IResourceFacade resource);
        bool CanBeProcessed(string fixtureId);
    }
}