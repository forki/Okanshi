using System.Linq;
using FluentAssertions;
using Xunit;

namespace Okanshi.Test
{
    public class MaxGaugeTest
    {
        [Fact]
        public void Gauge_tag_is_added_to_configuration()
        {
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));

            gauge.Config.Tags.Should().Contain(DataSourceType.Gauge);
        }

        [Fact]
        public void Initial_value_is_zero()
        {
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));

            gauge.GetValue().Should().Be(0);
        }

        [Fact]
        public void Supplying_value_greater_than_previously_record_updates_the_value()
        {
            const long expectedValue = 1000;
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));

            gauge.Set(expectedValue);

            gauge.GetValue().Should().Be(expectedValue);
        }

        [Fact]
        public void Supplying_value_less_than_previously_record_does_not_update_the_value()
        {
            const long maximumValue = 500;
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));
            gauge.Set(maximumValue);

            gauge.Set(100);

            gauge.GetValue().Should().Be(maximumValue);
        }

        [Fact]
        public void Reset_sets_the_value_to_zero()
        {
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));
            gauge.Set(100L);

            gauge.Reset();

            gauge.GetValue().Should().Be(0L);
        }

        [Fact]
        public void Get_and_reset_sets_the_value_to_zero()
        {
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));
            gauge.Set(100L);

            gauge.GetValueAndReset();

            gauge.GetValue().Should().Be(0L);
        }

        [Fact]
        public void Get_and_reset_gets_the_maximum_value()
        {
            const long expected = 100L;
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));
            gauge.Set(expected);

            gauge.GetValueAndReset().Should().Be(expected);
        }

        [Fact]
        public void Consists_of_a_single_monitor()
        {
            var gauge = new MaxGauge(MonitorConfig.Build("Test"));

            gauge.GetAllMonitors().Should().HaveCount(1);
            gauge.GetAllMonitors().Single().Should().BeSameAs(gauge);
        }
    }
}