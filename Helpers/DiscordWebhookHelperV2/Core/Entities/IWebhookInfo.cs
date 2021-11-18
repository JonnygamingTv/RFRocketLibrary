using RFRocketLibrary.Helpers.DiscordWebhookHelperV2.Action.Entities.Rest.ActionClassified;
using RFRocketLibrary.Helpers.DiscordWebhookHelperV2.Core.Enums;

namespace RFRocketLibrary.Helpers.DiscordWebhookHelperV2.Core.Entities
{
    /// <summary>
    ///     Received information about webhook in a direct GET request.
    /// </summary>
    public interface IWebhookInfo
    {
        /// <summary>
        ///     The type of webhook we are dealing with.
        /// </summary>
        WebhookType Type { get; }

        /// <summary>
        ///     Snowflake webhook id.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     Snowflake converted to <see cref="ulong"/>.
        /// </summary>
        ulong IdULong { get; }

        /// <summary>
        ///     Webhook name.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Avatar id for webhook.
        /// </summary>
        string? AvatarId { get; }

        /// <summary>
        ///     Gets the avatar url for webhook.
        /// </summary>
        string? AvatarUrl { get; }

        /// <summary>
        ///     Snowflake webhook id of the channel it interacts with.
        /// </summary>
        string ChannelId { get; }

        /// <summary>
        ///     Snowflake converted to <see cref="ulong"/>.
        /// </summary>
        ulong ChannelIdUlong { get; }

        /// <summary>
        ///     Snowflake webhook guild id where it is hosted.
        /// </summary>
        string GuildId { get; }

        /// <summary>
        ///     Snowflake converted to <see cref="ulong"/>.
        /// </summary>
        ulong GuildIdULong { get; }

        /// <summary>
        ///     Webhook token.
        /// </summary>
        string Token { get; }

        /// <summary>
        ///     Gets a webhook avatar.
        /// </summary>
        IAvatarAction GetAvatar();
    }
}