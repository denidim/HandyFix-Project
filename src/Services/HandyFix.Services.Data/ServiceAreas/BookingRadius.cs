namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The postcode districts online booking takes besides the area pages' own: every district
    /// with at least one postcode within 15 miles of the Chessington base, the company's decision
    /// of 11 November 2026 (PROJECT_STATE.md Section 3ek). Built from a 15-mile radius search of
    /// postcodes around the base, keeping only the district part (KT9, TW1), since that is the part
    /// the booking form checks. New houses get new postcodes but almost never a new district, so
    /// the list does not go stale. A district on the edge can reach a little past 15 miles, which
    /// the company accepts.
    /// </summary>
    internal static class BookingRadius
    {
        // 251 districts, from 133892 postcodes.
        public static readonly IReadOnlyList<string> Districts = Array.AsReadOnly(new[]
        {
            "BR1", "BR2", "BR3", "BR4", "BR7",
            "CR0", "CR2", "CR3", "CR4", "CR5", "CR6", "CR7", "CR8", "CR9", "CR90",
            "E1", "E1W", "E98",
            "EC1A", "EC1M", "EC1N", "EC1P", "EC1R", "EC1V", "EC1Y", "EC2A", "EC2M", "EC2N", "EC2P", "EC2R", "EC2V",
            "EC2Y", "EC3A", "EC3M", "EC3N", "EC3P", "EC3R", "EC3V", "EC4A", "EC4M", "EC4N", "EC4P", "EC4R", "EC4V",
            "EC4Y",
            "GU1", "GU2", "GU3", "GU4", "GU5", "GU6", "GU18", "GU20", "GU21", "GU22", "GU23", "GU24", "GU25",
            "HA0", "HA1", "HA2", "HA3", "HA4", "HA5", "HA9",
            "KT1", "KT2", "KT3", "KT4", "KT5", "KT6", "KT7", "KT8", "KT9", "KT10", "KT11", "KT12", "KT13", "KT14",
            "KT15", "KT16", "KT17", "KT18", "KT19", "KT20", "KT21", "KT22", "KT23", "KT24",
            "N1", "N1C", "N7", "N8",
            "NW1", "NW1W", "NW2", "NW3", "NW4", "NW5", "NW6", "NW8", "NW9", "NW10", "NW11", "NW26",
            "RH1", "RH2", "RH3", "RH4", "RH5", "RH6", "RH8", "RH9",
            "SE1", "SE1P", "SE4", "SE5", "SE6", "SE8", "SE10", "SE11", "SE12", "SE13", "SE14", "SE15", "SE16",
            "SE17", "SE19", "SE20", "SE21", "SE22", "SE23", "SE24", "SE25", "SE26", "SE27",
            "SL0", "SL1", "SL2", "SL3", "SL4", "SL5", "SL95",
            "SM1", "SM2", "SM3", "SM4", "SM5", "SM6", "SM7",
            "SW1A", "SW1E", "SW1H", "SW1P", "SW1V", "SW1W", "SW1X", "SW1Y", "SW2", "SW3", "SW4", "SW5", "SW6",
            "SW7", "SW8", "SW9", "SW10", "SW11", "SW12", "SW13", "SW14", "SW15", "SW16", "SW17", "SW18", "SW19",
            "SW20", "SW95",
            "TN16",
            "TW1", "TW2", "TW3", "TW4", "TW5", "TW6", "TW7", "TW8", "TW9", "TW10", "TW11", "TW12", "TW13", "TW14",
            "TW15", "TW16", "TW17", "TW18", "TW19", "TW20",
            "UB1", "UB2", "UB3", "UB4", "UB5", "UB6", "UB7", "UB8", "UB9", "UB10", "UB11", "UB18",
            "W1A", "W1B", "W1C", "W1D", "W1F", "W1G", "W1H", "W1J", "W1K", "W1S", "W1T", "W1U", "W1W", "W2", "W3",
            "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12", "W13", "W14",
            "WC1A", "WC1B", "WC1E", "WC1H", "WC1N", "WC1R", "WC1V", "WC1X", "WC2A", "WC2B", "WC2E", "WC2H", "WC2N",
            "WC2R",
        });
    }
}
