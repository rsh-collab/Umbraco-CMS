﻿using System;
using System.Collections.Generic;
using System.Web.Security;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;
using Umbraco.Web.Models.ContentEditing;
using umbraco;
using System.Linq;

namespace Umbraco.Web.Models.Mapping
{
    /// <summary>
    /// Declares model mappings for members.
    /// </summary>
    internal class MemberModelMapper : MapperConfiguration
    {
        public override void ConfigureMappings(IConfiguration config, ApplicationContext applicationContext)
        {
            //FROM IMember TO MediaItemDisplay
            config.CreateMap<IMember, MemberDisplay>()
                  .ForMember(
                      dto => dto.Owner,
                      expression => expression.ResolveUsing<OwnerResolver<IMember>>())
                  .ForMember(
                      dto => dto.Icon,
                      expression => expression.MapFrom(content => content.ContentType.Icon))
                  .ForMember(
                      dto => dto.ContentTypeAlias,
                      expression => expression.MapFrom(content => content.ContentType.Alias))
                  .ForMember(
                      dto => dto.ContentTypeName,
                      expression => expression.MapFrom(content => content.ContentType.Name))
                  .ForMember(display => display.Properties, expression => expression.Ignore())
                  .ForMember(display => display.Tabs,
                             expression => expression.ResolveUsing(
                                 new TabsAndPropertiesResolver(
                                     //do no map this properties (currently anyways, they were never there in 6.x)
                                     Constants.Conventions.Member.StandardPropertyTypeStubs.Select(x => x.Value.Alias))))
                  .AfterMap(MapGenericCustomProperties);

            //FROM IMember TO ContentItemBasic<ContentPropertyBasic, IMember>
            config.CreateMap<IMember, ContentItemBasic<ContentPropertyBasic, IMember>>()
                  .ForMember(
                      dto => dto.Owner,
                      expression => expression.ResolveUsing<OwnerResolver<IMember>>())
                  .ForMember(
                      dto => dto.Icon,
                      expression => expression.MapFrom(content => content.ContentType.Icon))
                  .ForMember(
                      dto => dto.ContentTypeAlias,
                      expression => expression.MapFrom(content => content.ContentType.Alias));

            //FROM IMember TO ContentItemDto<IMember>
            config.CreateMap<IMember, ContentItemDto<IMember>>()
                  .ForMember(
                      dto => dto.Owner,
                      expression => expression.ResolveUsing<OwnerResolver<IMember>>())
                    //do no map the custom member properties (currently anyways, they were never there in 6.x)
                  .ForMember(dto => dto.Properties, expression => expression.ResolveUsing<MemberDtoPropertiesValueResolver>());
        }

        /// <summary>
        /// Maps the generic tab with custom properties for content
        /// </summary>
        /// <param name="member"></param>
        /// <param name="display"></param>
        private static void MapGenericCustomProperties(IMember member, MemberDisplay display)
        {
            TabsAndPropertiesResolver.MapGenericProperties(
                member, display,
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}login", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("login"),
                        Value = display.Username,
                        View = "textbox",
                        Config = new Dictionary<string, object> {{"IsRequired", true}}
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}email", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("general", "email"),
                        Value = display.Email,
                        View = "email",
                        Config = new Dictionary<string, object> {{"IsRequired", true}}
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}password", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("password"),
                        //NOTE: The value here is a json value - but the only property we care about is the generatedPassword one if it exists - this is the only value we'll ever supply to the front-end
                        Value = new Dictionary<string, object>
                            {
                                {"generatedPassword", member.AdditionalData.ContainsKey("GeneratedPassword") ? member.AdditionalData["GeneratedPassword"] : null}
                            },
                        //TODO: Hard coding this because the changepassword doesn't necessarily need to be a resolvable (real) property editor
                        View = "changepassword",
                        Config = new Dictionary<string, object>(
                    //initialize the dictionary with the configuration from the default membership provider
                    Membership.Provider.GetConfiguration())
                            {
                                //the password change toggle will only be displayed if there is already a password assigned.
                                {"hasPassword", member.Password.IsNullOrWhiteSpace() == false}
                            }
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}membergroup", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("content", "membergroup"),
                        Value = GetMemberGroupValue(display.Username),
                        View = "membergroups",
                        Config = new Dictionary<string, object> {{"IsRequired", true}}
                    });

        }        

        internal static IDictionary<string, bool> GetMemberGroupValue(string username)
        {
            var result = new Dictionary<string, bool>();
            foreach (var role in Roles.GetAllRoles().Distinct())
            {
                result.Add(role, false);
                // if a role starts with __umbracoRole we won't show it as it's an internal role used for public access
                if (role.StartsWith(Constants.Conventions.Member.InternalRolePrefix) == false)
                {
                    if (Roles.IsUserInRole(username, role))
                    {
                        result[role] = true;
                    }                        
                }
            }
            return result;
        }

        /// <summary>
        /// This ensures that the custom membership provider properties are not mapped (currently since they weren't there in v6)
        /// </summary>
        /// <remarks>
        /// Because these properties don't exist on the form, if we don't remove them for this map we'll get validation errors when posting data
        /// </remarks>
        internal class MemberDtoPropertiesValueResolver : ValueResolver<IMember, IEnumerable<ContentPropertyDto>>
        {
            protected override IEnumerable<ContentPropertyDto> ResolveCore(IMember source)
            {
                var exclude = Constants.Conventions.Member.StandardPropertyTypeStubs.Select(x => x.Value.Alias).ToArray();
                return source.Properties
                             .Where(x => exclude.Contains(x.Alias) == false)
                             .Select(Mapper.Map<Property, ContentPropertyDto>);
            }
        }

    }
}