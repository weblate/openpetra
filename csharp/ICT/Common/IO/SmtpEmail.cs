/*************************************************************************
 *
 * DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * @Authors:
 *       timop
 *
 * Copyright 2004-2009 by OM International
 *
 * This file is part of OpenPetra.org.
 *
 * OpenPetra.org is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenPetra.org is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
 *
 ************************************************************************/
using System;
using System.Net;
using System.Net.Mail;
using Ict.Common;

namespace Ict.Common.IO
{
    /// <summary>
    /// this is a small wrapper around the .net SMTP Email services
    /// </summary>
    public class TSmtpSender
    {
        private SmtpClient FSmtpClient;

        /// <summary>
        /// setup the smtp client
        /// </summary>
        public TSmtpSender(string ASMTPHost, int ASMTPPort, bool AEnableSsl, string AUsername, string APassword, string AOutputEMLToDirectory)
        {
            //Set up SMTP client
            FSmtpClient = new SmtpClient();

            if (AOutputEMLToDirectory.Length > 0)
            {
                FSmtpClient.PickupDirectoryLocation = AOutputEMLToDirectory;
                FSmtpClient.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            }
            else
            {
                FSmtpClient.Host = ASMTPHost;
                FSmtpClient.Port = ASMTPPort;
                FSmtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                FSmtpClient.UseDefaultCredentials = false;
                FSmtpClient.Credentials = new NetworkCredential(AUsername, APassword);
                FSmtpClient.EnableSsl = AEnableSsl;
            }
        }

        /// <summary>
        /// setup the smtp client from the config file or command line parameters
        /// </summary>
        public TSmtpSender()
        {
            TAppSettingsManager settings = new TAppSettingsManager();

            //Set up SMTP client
            FSmtpClient = new SmtpClient();

            if (settings.HasValue("OutputEMLToDirectory"))
            {
                FSmtpClient.PickupDirectoryLocation = settings.GetValue("OutputEMLToDirectory");
                FSmtpClient.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            }
            else
            {
                FSmtpClient.Host = settings.GetValue("SmtpHost");
                FSmtpClient.Port = settings.GetInt16("SmtpPort", 25);
                FSmtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                FSmtpClient.Credentials = new NetworkCredential(
                    settings.GetValue("SmtpUser", ""),
                    settings.GetValue("SmtpPassword", ""));
                FSmtpClient.EnableSsl = settings.GetBoolean("SmtpEnableSsl", false);
            }
        }

        /// <summary>
        /// Send an email message
        /// </summary>
        /// <param name="AEmail">on successful sending, the header is modified with the sent date</param>
        /// <returns>true if email was sent successfully</returns>
        public bool SendMessage(ref MailMessage AEmail)
        {
            if (AEmail.Headers.Get("Date-Sent") != null)
            {
                // don't send emails several times
                return false;
            }

            //Attempt to send the email
            try
            {
                AEmail.IsBodyHtml = AEmail.Body.ToLower().Contains("<html>");

                FSmtpClient.Send(AEmail);

                AEmail.Headers.Add("Date-Sent", DateTime.Now.ToString());
                return true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("There was an error while sending the message:\n\n" + ex.ToString());
                throw ex;
            }
        }
    }
}