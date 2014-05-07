//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model.Enums;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerTests
    {

        public string _fixtureId = "y9s1fVzAoko805mzTnnTRU_CQy8";

        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopListening()
        {
            var fixtureSnapshot = new Fixture { Id="TestId", MatchStatus = "30", Sequence = 1 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());

            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(-1);
            

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
        }

        [Category("Adapter")]
        [Test]
        public void ShouldNotProcessDeltaAsSequenceIsSmaller()
        {
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();
            var fixtureSnapshot = new Fixture { Id = "TestId", Epoch = 0, MatchStatus = "30", Sequence = 11 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.Content).Returns(new Summary());
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(10);
            marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();

            resource.Raise(r => r.StreamEvent += null, new StreamEventArgs(fixtureDeltaJson));

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.VerifyAll();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldSequenceAndEpochBeValid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            int matchStatusDelta = 40;
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(matchStatus: matchStatusDelta);
            
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = "30", Sequence = 1 };

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            resource.Setup(r => r.MatchStatus).Returns((MatchStatus)matchStatusDelta);
            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));
            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
             
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState("Football", It.IsAny<string>(), 2, resource.Object.MatchStatus), Times.Once());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldNotProcessStreamUpdateIfSnapshotWasProcessed()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 20, 0, 30));

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();
            
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            connector.Verify(x=> x.ProcessStreamUpdate(It.IsAny<Fixture>(),It.IsAny<bool>()),Times.Never());

        }

        [Test]
        [Category("Adapter")]
        public void ShouldSequenceBeInvalid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),It.IsAny<MatchStatus>()), Times.Once());
        }

        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithInvalidSequenceTest()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()));

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
            
            listener.Start();
                        
            //current sequence is 19, 21 is invalid
            listener.CheckStreamHealth(30000, 21);

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            
            resource.Verify(r=> r.GetSnapshot(),Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithShouldBeSynchronisedWithUpdatesTest()
        {
            
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 18, 0, 30));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()));

            // the Check Health needs to be done while update is still being processed
            connector.Setup(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Callback(() => listener.CheckStreamHealth(30000, 23));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();

            var nextSequenceFixtureDeltaJson = TestHelper.GetRawStreamMessage();
            resource.Raise(r => r.StreamEvent += null, new StreamEventArgs(nextSequenceFixtureDeltaJson));
            
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(1));

            resource.Verify(r => r.GetSnapshot(), Times.Once);
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithValidSequenceTest()
        {

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();
            
            listener.CheckStreamHealth(30000, 19);

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeValidAsStartTimeHasChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Start Time has changed

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAsCurrentIsGreater()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Fixture Deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //listener.IsFixtureEnded.Should().BeTrue();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
        }
        
        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAndFixtureEndedAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2, matchStatus:50, epochChangeReason:10); // deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //should be irrelevant
            //listener.IsFixtureEnded.Should().BeTrue();

            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), true), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldProcessSnapshopWhenReconnecting()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var snapshot = TestHelper.GetSnapshotJson();
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.ResourceOnStreamConnected(this, EventArgs.Empty);
            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);
            listener.ResourceOnStreamConnected(this, EventArgs.Empty);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Exactly(2));
            resource.Verify(r => r.GetSnapshot(), Times.Exactly(2));
        }

        [Test]
        [Category("Adapter")]
        public void ShouldNotStreamOnSetupState()
        {
            // here I want to test that if a resource is in
            // Setup state, the streaming should not start

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            // STEP 3: check that is not streaming
            listener.IsStreaming.Should().BeFalse();

            // STEP 4: we do the same but with status Ready

            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Ready);

            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.IsStreaming.Should().BeFalse();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldStreamOnInRunningState()
        {
            // here I want to test that if a resource is in
            // InRunning state, the streaming should start

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            // STEP 3: check that is streaming
            listener.IsStreaming.Should().BeTrue();

        }

        [Test]
        [Category("Adapter")]
        public void ShouldStartStreamingOnMatchStatusChange()
        {
            // here I want to test that when a resource
            // pass from "InSetup" to "InRunning", the stream
            // should start

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            // STEP 3: check that is NOT streaming
            listener.IsStreaming.Should().BeFalse();

            // STEP 4: put the resource object in "InRunningState" and notify the listener
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            listener.UpdateResourceState(resource.Object);

            // STEP 5: check that the streaming is activated
            listener.IsStreaming.Should().BeTrue();

        }

        [Test]
        [Category("Adapter")]
        public void ShouldSuspendOnEarlyDisconnection()
        {
            // here I want to test that in the case that the streamlistener
            // receives an early disconnect event, the fixture is suspend.
            // Moreover, I want to test that the adapter is able to
            // recognize this situation by calling CheckHealthStream

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            Fixture fixture = new Fixture {Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString()};

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            // just to be sure that we are streaming
            listener.IsStreaming.Should().BeTrue();

            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

            // STEP 3: Check the resoults
            listener.IsStreaming.Should().BeFalse();
            listener.IsFixtureEnded.Should().BeFalse();

            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Once, "StreamListener did not suspend the fixture");
            listener.CheckStreamHealth(1, 1).Should().BeFalse();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldNotSuspendFixtureOnProperDisconnection()
        {
            // here I want to test that in the case that the streamlistener
            // receives an a disconnect event due the fact that the fixture
            // is ended, the stream listener do not generate a suspend request
            
            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            Fixture update = new Fixture {
                Id = "ABC", 
                Sequence = 2, 
                MatchStatus = ((int) MatchStatus.MatchOver).ToString(), 
                Epoch = 2, 
                LastEpochChangeReason = new [] { (int) EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage {Content = update};

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            // just to be sure that we are streaming
            listener.IsStreaming.Should().BeTrue();

            // send the update that contains the match status change
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

            // STEP 3: Check the resoults
            listener.IsStreaming.Should().BeFalse();
            listener.IsFixtureEnded.Should().BeTrue();
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Once, "StreamListener did not suspend the fixture");
            listener.CheckStreamHealth(1, 1).Should().BeFalse();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldStartStreamingOnlyOnce()
        {
            // here I want to test that I can call
            // StreamListener.Start() how many times
            // I want, that only one connection is actually
            // made to the stream server

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);


            // STEP 3: raise 100 calls at random (short) delayes to listener.Start()
            for (int i = 0; i < 100; i++)
            {
                Task.Delay(new Random(DateTime.Now.Millisecond).Next(100)).ContinueWith(x => listener.Start());
            }

            // give a change to the thread to start and finish
            Thread.Sleep(1000);

            // STEP 4: verify that only one call to resource.StartStreaming() has been made...
            resource.Verify(x => x.StartStreaming(), Times.Once, "Streaming must start only once!");

            // ... and we are indeed streaming
            listener.IsStreaming.Should().BeTrue();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldReturnOnAlreadyProcessedSequence()
        {
            // here I want to test that, if for some unknown reason,
            // we receive an update that contains a sequence number
            // lesser than the last processed sequence, then the update
            // should be discarded

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            // Please note Sequence = 3
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 3, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            // ...and Sequence = 2
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString()
            };

            StreamMessage message = new StreamMessage {Content = update};

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never, "Update should be processed");
        }

        [Test]
        [Category("Adapter")]
        public void ShouldGetSnasphotOnInvalidSequence()
        {
            // here I want to test that, if for some unknown reasons
            // I miss an update (I get an update with 
            // sequence > last processed sequence + 1) then I should
            // get a snapshot

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.Paused).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a the epoch change
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessStreamUpdate is never called!
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Once);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The streamlistener was supposed to acquire a new snapshot"); 
        }

        [Test]
        [Category("Adapter")]
        public void ShouldGetASnapshotOnInvalidEpoch()
        {
            // here I want to test that if, for some reasons,
            // we get a snapshot with a epoch change, then
            // a snapshot must be retrieved.
            //
            // Pay attention that IsStartTimeChanged is considered
            // an epoch change for which is not necessary
            // retrieve a new snapshot


            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            // Please note Sequence = 1
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            // ...and Sequence = 3
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 3,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessStreamUpdate is never called!
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Once);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The streamlistener was supposed to acquire a new snapshot"); 
        }

        [Test]
        [Category("Adapter")]
        public void ShouldNOTGetASnapshotOnValidEpoch()
        {
            // here I want to test that if, a new snapshot
            // is not retrieved when there is no epoch change

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessSnapshot is called only once!
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Never);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
            resource.Verify(x => x.GetSnapshot(), Times.Once, "The streamlistener was NOT supposed to acquire a new snapshot");
        }

        [Test]
        [Category("Adapter")]
        public void ShouldStopStreamingIfFixtureIsEnded()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<IObjectProvider<IMarketStateCollection>> provider = new Mock<IObjectProvider<IMarketStateCollection>>();

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2, 
                LastEpochChangeReason = new [] { (int) EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            provider.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessSnapshot is called only once!
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Never);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
            resource.Verify(x => x.GetSnapshot(), Times.Once, "The streamlistener was supposed to acquire a new snapshot");
        }

    
    }
}
