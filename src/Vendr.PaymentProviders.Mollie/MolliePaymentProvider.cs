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

using MollieAmmount = Mollie.Api.Models.Amount;
using MollieLocale = Mollie.Api.Models.Payment.Locale;
using MolliePaymentStatus = Mollie.Api.Models.Payment.PaymentStatus;

namespace Vendr.PaymentProviders.Mollie
{
    [PaymentProvider("mollie", "Mollie", "Basic payment provider for payments that will be processed via an external mollie system", Icon = "icon-invoice")]
    public class MolliePaymentProvider : PaymentProviderBase<MollieSettings>
    {
        public MolliePaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool CanCancelPayments => true;
        public override bool CanCapturePayments => true;
        public override bool FinalizeAtContinueUrl => true;

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
                RedirectUrl = callbackUrl + "?redirect=true", // Explicitly redirect to the callback URL as this will need to do more processing to know where to redirect to
                WebhookUrl = callbackUrl,
                Locale = !string.IsNullOrWhiteSpace(settings.Locale) ? settings.Locale : MollieLocale.en_US
            };

            var mollieOrderResult = mollieOrderClient.CreateOrderAsync(mollieOrderRequest).GetAwaiter().GetResult();

            return new PaymentFormResult
            {
                Form = new PaymentForm(mollieOrderResult.Links.Checkout.Href, FormMethod.Get),
                MetaData = new Dictionary<string, string>()
                {
                    { "mollieOrderId", mollieOrderResult.Id },
                    { "vendrCancelUrl", cancelUrl },
                    { "vendrContinueUrl", continueUrl }
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
            var baseUrl = request.Url.GetLeftPart(UriPartial.Authority);

            var mollieOrderId = order.Properties["mollieOrderId"];
            var mollieOrderClient = new OrderClient(settings.TestMode ? settings.TestApiKey : settings.LiveApiKey);
            var mollieOrder = mollieOrderClient.GetOrderAsync(mollieOrderId, true).GetAwaiter().GetResult();

            if (mollieOrder.Embedded.Payments.All(x => x.Status == MolliePaymentStatus.Canceled))
            {
                response.Headers.Location = new Uri(baseUrl + "/" + order.Properties["cancelUrl"]);
            }
            else
            {
                response.Headers.Location = new Uri(baseUrl + "/" + order.Properties["continueUrl"]);
            }

            return new CallbackResult
            {
                HttpResponse = response
            };
        }

        private CallbackResult ProcessWebhookCallback(OrderReadOnly order, HttpRequestBase request, MollieSettings settings)
        {
            return CallbackResult.Ok(new TransactionInfo
            {
                AmountAuthorized = order.TotalPrice.Value.WithTax,
                TransactionFee = 0m,
                TransactionId = Guid.NewGuid().ToString("N"),
                PaymentStatus = PaymentStatus.Authorized
            });
        }

        public override ApiResult CancelPayment(OrderReadOnly order, MollieSettings settings)
        {
            return new ApiResult
            {
                TransactionInfo = new TransactionInfoUpdate()
                {
                    TransactionId = order.TransactionInfo.TransactionId,
                    PaymentStatus = PaymentStatus.Cancelled
                }
            };
        }

        public override ApiResult CapturePayment(OrderReadOnly order, MollieSettings settings)
        {
            return new ApiResult
            {
                TransactionInfo = new TransactionInfoUpdate()
                {
                    TransactionId = order.TransactionInfo.TransactionId,
                    PaymentStatus = PaymentStatus.Captured
                }
            };
        }
    }

    public class MollieVendrMetadata
    {
        public string OrderReference { get; set; }
        public string CancelUrl { get; set; }
        public string ContinueUrl { get; set; }
    }
}
