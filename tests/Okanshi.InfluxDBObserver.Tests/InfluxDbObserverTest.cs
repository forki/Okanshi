﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InfluxDB.WriteOnly;
using NSubstitute;
using Okanshi.Observers;
using Xunit;

namespace Okanshi.InfluxDBObserver.Tests
{
    public class InfluxDbObserverTest
    {
        private readonly IInfluxDbClient influxDbClient = Substitute.For<IInfluxDbClient>();

        [Fact]
        public void Poller_is_not_allowed_to_be_null()
        {
            Action action = () => new InfluxDbObserver(null, influxDbClient, new InfluxDbObserverOptions("anything"));
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Client_is_not_allowed_to_be_null()
        {
            Action action = () => new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), null,
                new InfluxDbObserverOptions("anything"));
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Options_is_not_allowed_to_be_null()
        {
            Action action = () => new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient, null);
            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Points_are_written_to_database_passed_into_options()
        {
            const string databaseName = "databaseName";
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions(databaseName));

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), databaseName, Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Database_selector_is_used_to_select_database()
        {
            const string expectedDatabase = "expectedDatabase";
            const string incorrectDatabase = "databaseName";
            var options = new InfluxDbObserverOptions(incorrectDatabase)
            {
                DatabaseSelector = _ => expectedDatabase
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.DidNotReceive().WriteAsync(Arg.Any<string>(), incorrectDatabase, Arg.Any<IEnumerable<Point>>());
            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), expectedDatabase, Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Point_are_by_default_written_to_autogen_retention_policy()
        {
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions("databaseName"));

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.Received(1).WriteAsync("autogen", Arg.Any<string>(), Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Retention_policy_is_used_to_select_retention_policy()
        {
            const string expectedRetentionPolicy = "expectedRetentionPolicy";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                RetentionPolicySelector = (metric, database) => expectedRetentionPolicy,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.DidNotReceive().WriteAsync("autogen", Arg.Any<string>(), Arg.Any<IEnumerable<Point>>());
            influxDbClient.Received(1).WriteAsync(expectedRetentionPolicy, Arg.Any<string>(),
                Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Tags_can_be_converted_to_fields_as_defined_by_options()
        {
            const string tagName = "tag";
            const string tagValue = "100";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(point => point.Tags.All(tag => tag.Key != tagName) &&
                                                                             point.Fields.Any(
                                                                                 field => field.Key == tagName && (int)field.Value ==
                                                                                          Convert.ToInt32(tagValue)))));
        }

        [Fact]
        public void Tags_can_be_ignored_as_defined_by_options()
        {
            const string tagName = "tag";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagsToIgnore = new List<string> { tagName }
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, "100"), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(point => point.Tags.All(tag => tag.Key != tagName))));
        }

        [Fact]
        public void Default_measurement_name_is_metric_name()
        {
            const string databaseName = "databaseName";
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions(databaseName));

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == "name")));
        }

        [Fact]
        public void Measurement_name_selector_is_used_to_select_measurement_name()
        {
            const string expectedMeasurementName = "expectedMeasurementName";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                MeasurementNameSelector = metric => expectedMeasurementName
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], 0, new Metric[0]) });

            influxDbClient.Received(1).WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Is<IEnumerable<Point>>(x => x.All(y => y.Measurement == expectedMeasurementName)));
        }

        [Fact]
        public void Tags_can_be_converted_to_float()
        {
            const string tagName = "tag";
            const string tagValue = "10.2";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == tagName && (float)field.Value == Convert.ToSingle(tagValue)))));
        }

        [Fact]
        public void Tags_can_be_converted_to_boolean()
        {
            const string tagName = "tag";
            const string tagValue = "true";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == tagName && (bool)field.Value == Convert.ToBoolean(tagValue)))));
        }

        [Fact]
        public void Tags_can_be_converted_to_string()
        {
            const string tagName = "tag";
            const string tagValue = "a random string";
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == tagName && (string)field.Value == tagValue))));
        }

        [Fact]
        public void Tags_can_be_converted_to_int()
        {
            const int expectedValue = 10;
            const string tagName = "tag";
            var tagValue = expectedValue.ToString();
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == tagName && (int)field.Value == expectedValue))));
        }

        [Fact]
        public void Tags_can_be_converted_to_long()
        {
            const long expectedValue = (long)int.MaxValue + 100;
            const string tagName = "tag";
            var tagValue = expectedValue.ToString();
            var options = new InfluxDbObserverOptions("databaseName")
            {
                TagToFieldSelector = tag => tag.Key == tagName,
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new[] { new Tag(tagName, tagValue), }, 0, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == tagName && (long)field.Value == expectedValue))));
        }

        [Fact]
        public void Submetrics_are_converted_to_fields_with_the_name_of_the_statistic()
        {
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                new InfluxDbObserverOptions("database"));
            var submetrics = new[]
            {
                new Metric("name", DateTimeOffset.Now, new[] { new Tag("statistic", "max") }, 200, new Metric[0])
            };

            observer.Update(new[]
                { new Metric("name", DateTimeOffset.UtcNow, Enumerable.Empty<Tag>().ToArray(), 100, submetrics) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.Count() == 1 &&
                                                         points.Any(p => p.Fields.Any(f => f.Key == "max" && (int)f.Value == 200) &&
                                                                         p.Fields.Any(f => f.Key == "value" && (int)f.Value == 100))));
        }

        [Fact]
        public void Fields_can_be_converted_to_long() {
            const long expectedValue = (long)int.MaxValue + 100;
            var options = new InfluxDbObserverOptions("databaseName");
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (long)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_can_be_converted_to_int() {
            const int expectedValue = 10;
            var options = new InfluxDbObserverOptions("databaseName");
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (int)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_can_be_converted_to_float() {
            const float expectedValue = 10.1f;
            var options = new InfluxDbObserverOptions("databaseName");
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (float)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_can_be_converted_to_boolean() {
            const bool expectedValue = true;
            var options = new InfluxDbObserverOptions("databaseName");
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (bool)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_can_be_converted_to_string() {
            const string expectedValue = "any string";
            var options = new InfluxDbObserverOptions("databaseName");
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (string)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_are_converted_according_to_options() {
            const float expectedValue = 1.0f;
            var options = new InfluxDbObserverOptions("databaseName")
            {
                ConvertFieldType = x => Convert.ToSingle(x)
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], (int)expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (float)field.Value == expectedValue))));
        }

        [Fact]
        public void Fields_converted_to_double_are_handled_as_float() {
            const float expectedValue = 1.0f;
            var options = new InfluxDbObserverOptions("databaseName")
            {
                ConvertFieldType = x => Convert.ToDouble(x)
            };
            var observer = new InfluxDbObserver(new MetricMonitorRegistryPoller(DefaultMonitorRegistry.Instance), influxDbClient,
                options);

            observer.Update(new[] { new Metric("name", DateTimeOffset.UtcNow, new Tag[0], expectedValue, new Metric[0]) });

            influxDbClient.Received(1)
                .WriteAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Is<IEnumerable<Point>>(points => points.All(
                        point => point.Fields.Any(field => field.Key == "value" && (float)field.Value == expectedValue))));
        }
    }
}