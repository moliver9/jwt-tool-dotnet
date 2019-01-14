﻿using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DotnetJwtTools
{    
    public class JwtTools
    {
        public bool isError = false;
        public string Error = null;

        //---------- BASIC PERMISSIONS -----------//
        private const string CNST_CREATE = "c";
        private const string CNST_UPDATE = "u";
        private const string CNST_DELETE = "d";
        private const string CNST_READ = "r";
        private const string CNST_EXECUTE = "x";

        // Product --> Object --> Permission --> Groups ... Api -> Operation -> read, Update -> Organizations..
        public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>> Permissions { get; set; }
        public bool IsAdmin { get; set; }
        public string Bearer { get; set; }
        public Dictionary<string, GroupTree> Groups { get; set; }
        public string MemberId { get; set; }


        public JwtTools(string pBearer, string pAdminGroup, UserConfig pConfig)
        {
            this._NewPermissionTable(pBearer, pAdminGroup, pConfig);
        }

        public JwtTools(string pBearer, UserConfig pConfig)
        {
            this._NewPermissionTable(pBearer, string.Empty, pConfig);
        }

        public JwtTools()
        {            
        }

        /// <summary>
        /// Create the PermissionTable and saves it
        /// </summary>
        /// <param name="pBearer"></param>
        /// <param name="pAdminGroup"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        private void _NewPermissionTable(string pBearer, string pAdminGroup, UserConfig pConfig)
        {            
            try
            {
                //List<Claim> claims = _ValidateJwtToken(pBearer, config);
                Tuple<string, string> iamMemberId = _GetIamAndMemberId(pBearer, pConfig);

                if (!string.IsNullOrEmpty(iamMemberId.Item1))
                {
                    string strJwt = iamMemberId.Item1;
                    string strMemberId = iamMemberId.Item2;

                    Jwt jwt = JsonConvert.DeserializeObject<Jwt>(strJwt);

                    this.Bearer = pBearer;
                    this.MemberId = strMemberId;
                    this.IsAdmin = false;
                    this.Permissions = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>();
                    this._BuildPermissions(new List<Jwt> { jwt }, new Dictionary<string, GroupTree>(), pAdminGroup);                    
                }
            }
            catch (Exception e)
            {
                this.Error = e.Message;
                this.isError = true;
                this.Permissions = null;
            }
        }


        #region JWT Validation
        //private static List<Claim> _ValidateJwtToken(string jwt, UserConfig config)
        //{
        //    var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(System.Text.Encoding.ASCII.GetBytes(config.ValidationPublicKey));
        //    TokenValidationParameters validationParameters =
        //        new TokenValidationParameters
        //        {
        //            ValidateIssuer = false,
        //            ValidateIssuerSigningKey = false,
        //            ValidateAudience = false,
        //            ValidateActor = false,
        //            ValidateLifetime = false,
        //            RequireExpirationTime = false,
        //            RequireSignedTokens = false
        //        };

        //    SecurityToken validatedToken;
        //    JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

        //    ClaimsPrincipal ret = handler.ValidateToken(jwt.Split(' ').Last(), validationParameters, out validatedToken);

        //    if (ret != null && ret.Identities.Count() > 0 && ret.Identities.First().IsAuthenticated && ret.Identities.First().Claims.Count() > 0)
        //    {
        //        return (List<Claim>)ret.Identities.First().Claims;
        //    }
        //    else
        //    {
        //        return null;
        //    }
        //} 
        #endregion

        //Return the IAM and MemberID claims value
        private static Tuple<string, string> _GetIamAndMemberId(string pJwt, UserConfig pConfig)
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = (JwtSecurityToken)handler.ReadToken(pJwt);

            string iam = string.Empty;
            string memberId = string.Empty;

            foreach (Claim claim in token.Claims)
            {
                if (claim.Type == $"{pConfig.ClaimUrl}{pConfig.ClaimIam}") iam = claim.Value;
                else if (claim.Type == $"{pConfig.ClaimUrl}{pConfig.ClaimMemberId}") memberId = claim.Value;

                if (iam != string.Empty && memberId != string.Empty) break;
            }
        
            return new Tuple<string, string>(iam, memberId);
        }

        /// <summary>
        /// Generates the permissions for a given groups an fill the group trees
        /// </summary>
        /// <param name="pGroups"></param>
        /// <param name="pTree"></param>
        /// <param name="pAdminGroup"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        private void _BuildPermissions(List<Jwt> pGroups, Dictionary<string, GroupTree> pTree, string pAdminGroup)
        {
            if (pGroups == null) return;

            foreach (Jwt group in pGroups)
            {
                //Check if the data is filled
                if (group.GroupCode == null) return;
                if (group.Type == null) return;

                //Fill the tree with the data on the group
                pTree[group.GroupCode] = new GroupTree { Groups = new Dictionary<string, GroupTree>(), GroupType = group.Type };

                //Check the Products
                bool isAdmin = _FillPermissionsFromProducts(group.GroupPermissions, this.Permissions, group.GroupCode, pAdminGroup);
                if (isAdmin) this.IsAdmin = true;

                //Call recursivity
                Dictionary<string, GroupTree> groupTree = pTree[group.GroupCode].Groups;
                this._BuildPermissions(group.GroupDescendants, groupTree, pAdminGroup);
            }
        }

        /// <summary>
        /// Check the Permissions in the Group.p nodes and add it in pPermissions
        /// </summary>
        /// <param name="pProducts"></param>
        /// <param name="pPermissions"></param>
        /// <param name="pGroup"></param>
        /// <param name="pAdminGroup"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        private bool _FillPermissionsFromProducts(Dictionary<string, Dictionary<string, List<string>>> pProducts, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>> pPermissions, string pGroup, string pAdminGroup)
        {
            if (pProducts == null) return false;
            bool ret = false;

            foreach (KeyValuePair<string, Dictionary<string, List<string>>> objects in pProducts)
            {
                //If we dont have the key, or the key with a null value, we initialize it
                if (!pPermissions.ContainsKey(objects.Key))
                {
                    pPermissions.Add(objects.Key, new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());
                }

                if (pPermissions[objects.Key] == null)
                {
                    pPermissions[objects.Key] = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                }

                Dictionary<string, Dictionary<string, Dictionary<string, string>>> p = pPermissions[objects.Key];

                if (objects.Value != null)
                {
                    foreach (KeyValuePair<string, List<string>> perms in objects.Value)
                    {
                        Tuple<bool, Dictionary<string, Dictionary<string, string>>> tuple;
                        if (!p.ContainsKey(perms.Key))
                        {
                            p.Add(perms.Key, new Dictionary<string, Dictionary<string, string>>());
                        }

                        tuple = _GetObjects(perms.Value, pGroup, p[perms.Key], pAdminGroup);
                        pPermissions[objects.Key][perms.Key] = tuple.Item2;
                        if (tuple.Item1) ret = true;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Get the permissions of an object and return the permissions and if the user is Admin
        /// </summary>
        /// <param name="pRoles"></param>
        /// <param name="pGroup"></param>
        /// <param name="pP"></param>
        /// <param name="pAdminGroup"></param>
        /// <remarks></remarks>
        /// <returns>A tuple with item1 = is Admin, Item2 = permissions.</returns>
        private Tuple<bool, Dictionary<string, Dictionary<string, string>>> _GetObjects(List<string> pRoles, string pGroup, Dictionary<string, Dictionary<string, string>> pP, string pAdminGroup)
        {
            bool isAdmin = false;

            foreach (string rol in pRoles)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> perm in _ExtractPermissions(rol))
                {
                    if (!pP.ContainsKey(perm.Key))
                    {
                        pP.Add(perm.Key, new Dictionary<string, string>());
                    }

                    if (!pP[perm.Key].ContainsKey(pGroup))
                    {
                        pP[perm.Key].Add(pGroup, string.Empty);
                    }                    
                }
            }

            if (pAdminGroup == pGroup || pP.ContainsKey(CNST_CREATE) && pP.ContainsKey(CNST_READ) && pP.ContainsKey(CNST_UPDATE) && pP.ContainsKey(CNST_DELETE))
            {
                isAdmin = true;
            }

            return new Tuple<bool, Dictionary<string, Dictionary<string, string>>>(isAdmin, pP);
        }

        /// <summary>
        /// Extracts the permission of a string
        /// </summary>
        /// <param name="pP"></param>
        /// <remarks></remarks>
        /// <returns>A dictionary with the permissions</returns>
        private Dictionary<string, Dictionary<string, string>> _ExtractPermissions(string p)
        {
            bool enabled = false;
            Dictionary<string, Dictionary<string, string>> ret = new Dictionary<string, Dictionary<string, string>>();

            for (int i = 0; i < p.Length; i++)
            {
                switch (p[i])
                {
                    case 'c':
                        ret.Add(CNST_CREATE, new Dictionary<string, string>());
                        break;
                    case 'r':
                        ret.Add(CNST_READ, new Dictionary<string, string>());
                        break;
                    case 'u':
                        ret.Add(CNST_UPDATE, new Dictionary<string, string>());
                        break;
                    case 'd':
                        ret.Add(CNST_DELETE, new Dictionary<string, string>());
                        break;
                    case '1':
                        enabled = true;
                        break;
                    default:
                        ret.Add(p[i].ToString(), new Dictionary<string, string>());
                        break;
                }
            }

            if (enabled) return ret;
            return new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Check if a group have a permision for a product and object 
        /// </summary>
        /// <param name="pProduct"></param>
        /// <param name="pObj"></param>
        /// <param name="pPermission"></param>
        /// <param name="pGroup"></param>
        /// <remarks></remarks>
        /// <returns>A boolean meaning if the group have permission</returns>
        public bool CheckPermission(string pProduct, string pObj, string pPermission, string pGroup)
        {
            if (this.Permissions == null) return false;
            if (this.IsAdmin) { return true; }
            //iam --> grp --> crud1 -> xtg
            if (this.Permissions.ContainsKey(pProduct) && this.Permissions[pProduct].ContainsKey(pObj))
            {
                foreach (KeyValuePair<string, Dictionary<string,string>> perms in this.Permissions[pProduct][pObj])
                {
                    if (perms.Key.Equals(pPermission))
                    {
                        foreach (KeyValuePair<string, string> _group in perms.Value)
                        {
                            if (pGroup.Equals(_group.Key)) return true;
                        }
                    }
                }
            }

            return false;
        }



        /// <summary>
        /// Add Permission to a group for a product and object
        /// </summary>
        /// <param name="pProduct"></param>
        /// <param name="pObj"></param>
        /// <param name="pPermission"></param>
        /// <param name="pGroup"></param>
        /// <remarks></remarks>
        /// <returns>A boolean meaning if the group have permission</returns>
        public bool AddPermission(string pProduct, string pObj, string pPermission, string pGroup)
        {
            //--- Check if Permissions is not null ----------------
            if (this.Permissions == null)
                this.Permissions = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>();

            //--- Check if we have the product -----------
            if (!this.Permissions.ContainsKey(pProduct))
                this.Permissions.Add(pProduct, new Dictionary<string, Dictionary<string, Dictionary<string, string>>>());

            //--- Check if we have the object ------------
            if (!this.Permissions[pProduct].ContainsKey(pObj))
                this.Permissions[pProduct].Add(pObj, new Dictionary<string, Dictionary<string, string>>());

            //--- Check if we have the permission --------
            if (!this.Permissions[pProduct][pObj].ContainsKey(pPermission))
                this.Permissions[pProduct][pObj].Add(pPermission, new Dictionary<string, string>());

            //--- Check if we have the group -------------
            if (!this.Permissions[pProduct][pObj][pPermission].ContainsKey(pGroup))
                this.Permissions[pProduct][pObj][pPermission].Add(pGroup, string.Empty);

            return true;
        }
    }
}
