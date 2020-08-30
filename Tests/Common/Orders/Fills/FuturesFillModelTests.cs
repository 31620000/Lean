﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using NUnit.Framework;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;
using QuantConnect.Tests.Common.Data;
using QuantConnect.Tests.Common.Securities;

namespace QuantConnect.Tests.Common.Orders.Fills
{
    [TestFixture]
    public class FuturesFillModelTests
    {
        private static readonly DateTime Noon = new DateTime(2014, 6, 24, 12, 0, 0);
        private static readonly TimeKeeper TimeKeeper = new TimeKeeper(Noon.ConvertToUtc(TimeZones.NewYork), new[] { TimeZones.NewYork });

        [Test]
        public void PerformsMarketFillBuy()
        {
            var model = new FuturesFillModel();
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, 100, Noon);
            var config = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 0, 0, 0, 101.123m, 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(security.Price, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = Math.Round(security.Price / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsMarketFillSell()
        {
            var model = new FuturesFillModel();
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, -100, Noon);
            var config = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 101.123m, 101.123m, 101.123m, 101.123m, 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(security.Price, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = (1 + Math.Round(security.Price / priceVariation)) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsLimitFillBuy()
        {
            var model = new FuturesFillModel();
            var order = new LimitOrder(Symbols.Fut_SPY_Mar19_2016, 100, 101.6m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 102m, 102m, 102m, 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103m, 101m, 102.3m, 100));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101m, 102m, 100m, 101.3m), 100,
                new Bar(103m, 104m, 102m, 103.3m), 100));

            fill = model.LimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(Math.Min(order.LimitPrice, security.High), fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = Math.Round(Math.Min(order.LimitPrice, security.High) / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsLimitFillSell()
        {
            var model = new FuturesFillModel();
            var order = new LimitOrder(Symbols.Fut_SPY_Mar19_2016, -100, 101.49m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 101m, 101m, 101m, 101m, 101m));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            var tradeBar = new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103m, 101m, 102.3m, 100);
            security.SetMarketPrice(tradeBar);
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101.6m, 102m, 101.6m, 101.6m), 100,
                new Bar(103m, 104m, 102m, 103.3m), 100));

            fill = model.LimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(Math.Max(order.LimitPrice, tradeBar.Low), fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = (1 + Math.Round(Math.Max(order.LimitPrice, tradeBar.Low) / priceVariation)) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsStopLimitFillBuy()
        {
            var model = new FuturesFillModel();
            var order = new StopLimitOrder(Symbols.Fut_SPY_Mar19_2016, 100, 101.5m, 101.7m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 100m, 100m, 100m, 100m, 100));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 102m, 102m, 102m, 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103.1m, 101m, 101.66m, 100));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101m, 102m, 100m, 100.66m), 100,
                new Bar(103m, 104m, 102m, 102.66m), 100));

            fill = model.StopLimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(Math.Min(security.High, order.LimitPrice), fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = Math.Round(Math.Min(security.High, order.LimitPrice) / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsStopLimitFillSell()
        {
            var model = new FuturesFillModel();
            var order = new StopLimitOrder(Symbols.Fut_SPY_Mar19_2016, -100, 101.75m, 101.40m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 102m, 102m, 102m, 100));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 101m, 101m, 101m, 101m, 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103m, 101m, 101.66m, 100));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101m, 102m, 100m, 100.66m), 100,
                new Bar(103m, 104m, 102m, 102.66m), 100));

            fill = model.StopLimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(Math.Max(security.Low, order.LimitPrice), fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = (1 + Math.Round(Math.Max(security.Low, order.LimitPrice) / priceVariation)) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsStopMarketFillBuy()
        {
            var model = new FuturesFillModel();
            var order = new StopMarketOrder(Symbols.Fut_SPY_Mar19_2016, 100, 101.5m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 101m, 101m, 101m, 101m, 100));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103m, 101m, 102.5m, 100));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101m, 102m, 100m, 101.5m), 100,
                new Bar(103m, 104m, 102m, 103.49m), 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            // this fills worst case scenario, so it's min of asset/stop price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(security.AskPrice, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = Math.Round(security.AskPrice / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsStopMarketFillSell()
        {
            var model = new FuturesFillModel();
            var order = new StopMarketOrder(Symbols.Fut_SPY_Mar19_2016, -100, 101.5m, Noon);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 102m, 102m, 102m, 100));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 102m, 103m, 101m, 101m, 100));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.Fut_SPY_Mar19_2016,
                new Bar(101m, 102m, 100m, 100.01m), 100,
                new Bar(103m, 104m, 102m, 102m), 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            // this fills worst case scenario, so it's min of asset/stop price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreNotEqual(security.BidPrice, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            var expected = (1 + Math.Round(security.BidPrice / priceVariation)) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsMarketOnOpenUsingOpenPrice()
        {
            var reference = new DateTime(2015, 06, 05, 9, 0, 0); // before market open
            var model = new FuturesFillModel();
            var order = new MarketOnOpenOrder(Symbols.Fut_SPY_Mar19_2016, 100, reference);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            var time = reference;
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(time, Symbols.Fut_SPY_Mar19_2016, 1m, 2m, 0.5m, 1.33m, 100));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);

            // market opens after 30min, so this is just before market open
            time = reference.AddMinutes(29);
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            security.SetMarketPrice(new TradeBar(time, Symbols.Fut_SPY_Mar19_2016, 1.33m, 2.75m, 1.15m, 1.45m, 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;
            Assert.AreEqual(0, fill.FillQuantity);

            var expected = 1.45m;
            // market opens after 30min
            time = reference.AddMinutes(30);
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(time, Symbols.Fut_SPY_Mar19_2016, expected, 2.0m, 1.1m, 1.40m, 100));
            security.SetMarketPrice(new QuoteBar(time, Symbols.Fut_SPY_Mar19_2016,
                new Bar(1.44m, 1.99m, 1.09m, 1.39m), 100,
                new Bar(1.46m, 2.01m, 1.11m, 1.41m), 100));

            fill = model.MarketOnOpenFill(security, order);
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreNotEqual(expected, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            expected = Math.Round(expected / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void PerformsMarketOnCloseUsingClosingPrice()
        {
            var reference = new DateTime(2015, 06, 05, 15, 0, 0); // before market close
            var model = new FuturesFillModel();
            var order = new MarketOnCloseOrder(Symbols.Fut_SPY_Mar19_2016, 100, reference);
            var configTradeBar = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            var time = reference;
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(time - configTradeBar.Increment, Symbols.Fut_SPY_Mar19_2016, 1m, 2m, 0.5m, 1.33m, 100, configTradeBar.Increment));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);

            // market closes after 60min, so this is just before market Close
            time = reference.AddMinutes(59);
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(time - configTradeBar.Increment, Symbols.Fut_SPY_Mar19_2016, 1.33m, 2.75m, 1.15m, 1.45m, 100, configTradeBar.Increment));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;
            Assert.AreEqual(0, fill.FillQuantity);

            var expected = 1.40m;
            // market closes
            time = reference.AddMinutes(60);
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(time - configTradeBar.Increment, Symbols.Fut_SPY_Mar19_2016, 1.45m, 2.0m, 1.1m, expected, 100, configTradeBar.Increment));
            security.SetMarketPrice(new QuoteBar(time - configTradeBar.Increment, Symbols.Fut_SPY_Mar19_2016,
                new Bar(1.44m, 1.99m, 1.09m, 1.39m), 100,
                new Bar(1.46m, 2.01m, 1.11m, 1.41m), 100, configTradeBar.Increment));

            fill = model.MarketOnCloseFill(security, order);
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreNotEqual(expected, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = security.SymbolProperties.MinimumPriceVariation;
            expected = Math.Round(expected / priceVariation) * priceVariation;
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [TestCase(OrderDirection.Buy)]
        [TestCase(OrderDirection.Sell)]
        public void MarketOrderFillsAtBidAsk(OrderDirection direction)
        {
            var exchangeHours = SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork);
            var quoteCash = new Cash(Currencies.USD, 1000, 1);
            var symbolProperties = new SymbolProperties("E-mini S&P 500 Futures", Currencies.USD, 50, 0.25m, 1);
            var config = new SubscriptionDataConfig(typeof(Tick), Symbols.Fut_SPY_Mar19_2016, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = new Future(exchangeHours, config, quoteCash, symbolProperties, ErrorCurrencyConverter.Instance, RegisteredSecurityDataTypesProvider.Null);

            var reference = DateTime.Now;
            var referenceUtc = reference.ConvertToUtc(TimeZones.NewYork);
            var timeKeeper = new TimeKeeper(referenceUtc);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var fillModel = new FuturesFillModel();

            const decimal bidPrice = 1.13739m;
            const decimal askPrice = 1.13746m;

            security.SetMarketPrice(new Tick(DateTime.Now, Symbols.Fut_SPY_Mar19_2016, bidPrice, askPrice));

            var quantity = direction == OrderDirection.Buy ? 1 : -1;
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, quantity, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            var expected = direction == OrderDirection.Buy ? askPrice : bidPrice;
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
            Assert.AreNotEqual(expected, fill.FillPrice);

            // Round to minimum price variation
            var priceVariation = symbolProperties.MinimumPriceVariation;
            expected = Math.Round(expected / priceVariation) * priceVariation + (direction == OrderDirection.Buy ? 0 : priceVariation);
            Assert.AreEqual(expected, fill.FillPrice);
        }

        [Test]
        public void ImmediateFillModelUsesPriceForTicksWhenBidAskSpreadsAreNotAvailable()
        {
            var noon = new DateTime(2014, 6, 24, 12, 0, 0);
            var timeKeeper = new TimeKeeper(noon.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var config = new SubscriptionDataConfig(typeof(Tick), Symbols.Fut_SPY_Mar19_2016, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(noon, Symbols.Fut_SPY_Mar19_2016, 101.123m, 101.123m, 101.123m, 101.123m, 100));

            // Add both a tradebar and a tick to the security cache
            // This is the case when a tick is seeded with minute data in an algorithm
            security.Cache.AddData(new TradeBar(DateTime.MinValue, Symbols.Fut_SPY_Mar19_2016, 1.0m, 1.0m, 1.0m, 1.0m, 1.0m));
            var tickPrice = 100.01m;
            security.Cache.AddData(new Tick(config, $"42525000,{tickPrice},10,A,@,0", DateTime.MinValue));

            var fillModel = new FuturesFillModel();
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, 1000, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
            Assert.AreNotEqual(tickPrice, fill.FillPrice);

            // The fill model should use the tick.Price rounded to 100
            Assert.AreEqual(100, fill.FillPrice);
        }

        [Test]
        public void ImmediateFillModelDoesNotUseTicksWhenThereIsNoTickSubscription()
        {
            var noon = new DateTime(2014, 6, 24, 12, 0, 0);
            var timeKeeper = new TimeKeeper(noon.ConvertToUtc(TimeZones.NewYork), new[] { TimeZones.NewYork });
            // Minute subscription
            var config = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(noon, Symbols.Fut_SPY_Mar19_2016, 101.123m, 101.123m, 101.123m, 101.123m, 100));

            // This is the case when a tick is seeded with minute data in an algorithm
            security.Cache.AddData(new TradeBar(DateTime.MinValue, Symbols.Fut_SPY_Mar19_2016, 1.0m, 1.0m, 1.0m, 1.0m, 1.0m));
            security.Cache.AddData(new Tick(config, "42525000,1000000,100,A,@,0", DateTime.MinValue));

            var fillModel = new FuturesFillModel();
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, 1000, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            // The fill model should use the tick.Price
            Assert.AreEqual(fill.FillPrice, 1.0m);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 290.50)]
        [TestCase(-100, 291.50)]
        public void LimitOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal limitPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var config = CreateTradeBarConfig(symbol);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (TradeBar)tradeBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new FuturesFillModel();
            var order = new LimitOrder(symbol, orderQuantity, limitPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            fill = fillModel.LimitFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(limitPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 291.50)]
        [TestCase(-100, 290.50)]
        public void StopMarketOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal stopPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var config = CreateTradeBarConfig(symbol);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (TradeBar)tradeBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new FuturesFillModel();
            var order = new StopMarketOrder(symbol, orderQuantity, stopPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            fill = fillModel.StopMarketFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(stopPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 291.50, 291.75)]
        [TestCase(-100, 290.50, 290.25)]
        public void StopLimitOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal stopPrice, decimal limitPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var config = CreateTradeBarConfig(symbol);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (TradeBar)tradeBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new FuturesFillModel();
            var order = new StopLimitOrder(symbol, orderQuantity, stopPrice, limitPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            fill = fillModel.StopLimitFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(limitPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 105)]
        [TestCase(-100, 100)]
        public void StopMarketOrderDoesNotFillWithOpenInterest(decimal orderQuantity, decimal openInterest)
        {
            var model = new FuturesFillModel();
            var order = new StopMarketOrder(Symbols.Fut_SPY_Mar19_2016, orderQuantity, 101.5m, Noon);
            var configTick = new SubscriptionDataConfig(typeof(Tick), Symbols.Fut_SPY_Mar19_2016, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = CreateSecurity(configTick);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var configOpenInterest = new SubscriptionDataConfig(configTick, typeof(OpenInterest));
            var configProvider = new MockSubscriptionDataConfigProvider(configOpenInterest);
            configProvider.SubscriptionDataConfigs.Add(configTick);

            security.SetMarketPrice(new Tick(Noon, Symbols.Fut_SPY_Mar19_2016, 101.5m, 101.5m, 101.5m));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.Update(new[] { new OpenInterest(Noon, Symbols.Fut_SPY_Mar19_2016, openInterest) }, typeof(Tick));

            fill = model.StopMarketFill(security, order);

            // Should not fill
            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);
        }

        [Test]
        public void MarketOrderFillWithStalePriceHasWarningMessage()
        {
            var model = new FuturesFillModel();
            var order = new MarketOrder(Symbols.Fut_SPY_Mar19_2016, -100, Noon.ConvertToUtc(TimeZones.NewYork).AddMinutes(61));
            var config = CreateTradeBarConfig(Symbols.Fut_SPY_Mar19_2016);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.Fut_SPY_Mar19_2016, 101.123m, 101.123m, 101.123m, 101.123m, 0, TimeSpan.Zero));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.IsTrue(fill.Message.Contains("Warning: fill at stale price"));
        }

        [TestCase(OrderDirection.Sell, 11)]
        [TestCase(OrderDirection.Buy, 21)]
        // uses the trade bar last close
        [TestCase(OrderDirection.Hold, 291)]
        public void PriceReturnsQuoteBarsIfPresent(OrderDirection orderDirection, decimal expected)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var configTradeBar = CreateTradeBarConfig(symbol);
            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            var quoteBar = new QuoteBar(time, symbol,
                new Bar(10, 15, 5, 11),
                100,
                new Bar(20, 25, 15, 21),
                100);
            security.SetMarketPrice(quoteBar);

            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var testFillModel = new TestFillModel();
            testFillModel.SetParameters(new FillModelParameters(security,
                null,
                configProvider,
                TimeSpan.FromDays(1)));

            //var result = testFillModel.GetPricesPublic(security, orderDirection);

            //Assert.AreEqual(expected, result.Current);
        }

        private SubscriptionDataConfig CreateTradeBarConfig(Symbol symbol)
        {
            return new SubscriptionDataConfig(typeof(TradeBar), symbol, Resolution.Minute, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
        }

        private Security CreateSecurity(SubscriptionDataConfig config)
        {
            return new Security(
                SecurityExchangeHoursTests.CreateUsEquitySecurityExchangeHours(),
                config,
                new Cash(Currencies.USD, 0, 1m),
                new SymbolProperties("E-mini S&P 500 Futures", Currencies.USD, 50, 0.25m, 1),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache()
            );
        }

        private class TestFillModel : FillModel
        {
            public void SetParameters(FillModelParameters parameters)
            {
                Parameters = parameters;
            }
        }
    }
}
