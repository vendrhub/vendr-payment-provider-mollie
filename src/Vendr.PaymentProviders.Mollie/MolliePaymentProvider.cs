using Mollie.Api.Client;
using Mollie.Api.Models.Order;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using System.Globalization;

using MollieAmmount = Mollie.Api.Models.Amount;
using MollieLocale = Mollie.Api.Models.Payment.Locale;
using MolliePaymentStatus = Mollie.Api.Models.Payment.PaymentStatus;
using MollieOrderStatus = Mollie.Api.Models.Order.OrderStatus;
using MollieOrderLineStatus = Mollie.Api.Models.Order.OrderLineStatus;

namespace Vendr.PaymentProviders.Mollie
{
    [PaymentProvider("mollie", "Mollie", "Basic payment provider for payments that will be processed via an external mollie system", Icon = "icon-invoice")]
    public class MolliePaymentProvider : PaymentProviderBase<MollieSettings>
    {
        private readonly IPaymentProviderUriResolver _uriResolver;

        public MolliePaymentProvider(VendrContext vendr,
            IPaymentProviderUriResolver uriResolver)
            : base(vendr)
        {
            _uriResolver = uriResolver;
        }

        public override bool CanFetchPaymentStatus => true;
        public override bool CanCancelPayments => false;
        public override bool CanRefundPayments => false;
        public override bool CanCapturePayments => false;
        public override bool FinalizeAtContinueUrl => false;

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, MollieSettings settings)
        {
            // Validate settings
            settings.MustNotBeNull("settings");

            if (settings.TestMode)
            {
                settings.TestApiKey.MustNotBeNull("settings.TestApiKey");
            }
            else
            {
                settings.LiveApiKey.MustNotBeNull("settings.LiveApiKey");
            }

            // Get entities
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var country = Vendr.Services.CountryService.GetCountry(order.PaymentInfo.CountryId.Value);

            // Create the order
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);

            var mollieOrderAddress = new OrderAddressDetails
            {
                GivenName = order.CustomerInfo.FirstName,
                FamilyName = order.CustomerInfo.LastName,
                Email = order.CustomerInfo.Email,
                Country = country.Code
            };

            if (!string.IsNullOrWhiteSpace(settings.BillingAddressLine1PropertyAlias))
                mollieOrderAddress.StreetAndNumber = order.Properties[settings.BillingAddressLine1PropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressCityPropertyAlias))
                mollieOrderAddress.City = order.Properties[settings.BillingAddressCityPropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressStatePropertyAlias))
                mollieOrderAddress.Region = order.Properties[settings.BillingAddressStatePropertyAlias];
            if (!string.IsNullOrWhiteSpace(settings.BillingAddressZipCodePropertyAlias))
                mollieOrderAddress.PostalCode = order.Properties[settings.BillingAddressZipCodePropertyAlias];

            // TODO: Populate order lines

            var mollieOrderRequest = new OrderRequest
            {
                Amount = new MollieAmmount(currency.Code.ToUpperInvariant(), order.TransactionAmount.Value),
                OrderNumber = order.OrderNumber,
                Metadata = order.GenerateOrderReference(),
                BillingAddress = mollieOrderAddress,
                RedirectUrl = callbackUrl + "?redirect=true", // Explicitly redirect to the callback URL as this will need to do more processing to decide where to redirect to
                WebhookUrl = callbackUrl,
                Locale = !string.IsNullOrWhiteSpace(settings.Locale) ? settings.Locale : MollieLocale.en_US
            };

            var mollieOrderResult = mollieOrderClient.CreateOrderAsync(mollieOrderRequest).GetAwaiter().GetResult();

            return new PaymentFormResult
            {
                Form = new PaymentForm(mollieOrderResult.Links.Checkout.Href, FormMethod.Get),
                MetaData = new Dictionary<string, string>()
                {
                    { "mollieOrderId", mollieOrderResult.Id }
                }
            };
        }

        public override string GetCancelUrl(OrderReadOnly order, MollieSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.CancelUrl.MustNotBeNull("settings.CancelUrl");

            return settings.CancelUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, MollieSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ErrorUrl.MustNotBeNull("settings.ErrorUrl");

            return settings.ErrorUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, MollieSettings settings)
        {
            settings.MustNotBeNull("settings");
            settings.ContinueUrl.MustNotBeNull("settings.ContinueUrl");

            return settings.ContinueUrl;
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, MollieSettings settings)
        {
            if (request.QueryString.AllKeys.Contains("redirect"))
            {
                return ProcessRedirectCallback(order, request, settings);
            }
            else
            {
                return ProcessWebhookCallback(order, request, settings);
            }
        }

        private CallbackResult ProcessRedirectCallback(OrderReadOnly order, HttpRequestBase request, MollieSettings settings)
        {
            var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Moved);

            var mollieOrderId = order.Properties["mollieOrderId"];
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true).GetAwaiter().GetResult();

            if (mollieOrder.Embedded.Payments.All(x => x.Status == MolliePaymentStatus.Canceled))
            {
                response.Headers.Location = new Uri(_uriResolver.GetCancelUrl(Alias, order.GenerateOrderReference(), Vendr.Security.HashProvider));
            }
            else
            {
                response.Headers.Location = new Uri(_uriResolver.GetContinueUrl(Alias, order.GenerateOrderReference(), Vendr.Security.HashProvider));
            }

            return new CallbackResult
            {
                HttpResponse = response
            };
        }

        private CallbackResult ProcessWebhookCallback(OrderReadOnly order, HttpRequestBase request, MollieSettings settings)
        {
            // Validate the ID from the webhook matches the orders mollieOrderId property
            var id = request.Form["id"];
            var mollieOrderId = order.Properties["mollieOrderId"];
            if (id != mollieOrderId)
                return CallbackResult.BadRequest();

            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true, true).GetAwaiter().GetResult();

            return CallbackResult.Ok(new TransactionInfo
            {
                AmountAuthorized = decimal.Parse(mollieOrder.Amount.Value, CultureInfo.InvariantCulture),
                TransactionFee = 0m,
                TransactionId = mollieOrderId,
                PaymentStatus = GetPaymentStatus(mollieOrder)
            });
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, MollieSettings settings)
        {
            var mollieOrderId = order.Properties["mollieOrderId"];
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true, true).GetAwaiter().GetResult();

            return new ApiResult
            {
                TransactionInfo = new TransactionInfoUpdate()
                {
                    TransactionId = order.TransactionInfo.TransactionId,
                    PaymentStatus = GetPaymentStatus(mollieOrder)
                }
            };
        }

        private PaymentStatus GetPaymentStatus(OrderResponse order)
        {
            // The order is refunded if the total refunded amount is
            // greater than or equal to the original amount of the order
            if (order.AmountRefunded != null)
            {
                var amount = decimal.Parse(order.Amount.Value, CultureInfo.InvariantCulture);
                var amountRefunded = decimal.Parse(order.AmountRefunded.Value, CultureInfo.InvariantCulture);

                if (amountRefunded >= amount)
                {
                    return PaymentStatus.Refunded;
                }
            }

            // If the order is in a shipping status, at least one of the order lines
            // should be in an authorized or paid status. If there are any authorized
            // rows, then set the whole order as authorized, otherwise we'll see it's 
            // captured.
            if (order.Status == MollieOrderStatus.Shipping)
            {
                if (order.Lines.Any(x => x.Status == MollieOrderLineStatus.Authorized))
                {
                    return PaymentStatus.Authorized;
                }
                else
                {
                    return PaymentStatus.Captured;
                }
            }

            if (order.Status == MollieOrderStatus.Paid || order.Status == MollieOrderStatus.Completed)
                return PaymentStatus.Captured;

            if (order.Status == MollieOrderStatus.Canceled)
                return PaymentStatus.Cancelled;

            if (order.Status == MollieOrderStatus.Authorized)
                return PaymentStatus.Authorized;

            return PaymentStatus.PendingExternalSystem;
        }
    }
}
