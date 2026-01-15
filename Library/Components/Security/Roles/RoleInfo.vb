'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'

Imports System
Imports System.Collections
Imports System.Configuration
Imports System.Data
Imports System.Xml.Serialization

Namespace DotNetNuke.Security.Roles

    ''' -----------------------------------------------------------------------------
    ''' Project:    DotNetNuke
    ''' Namespace:  DotNetNuke.Security.Roles
    ''' Class:      RoleInfo
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The RoleInfo class provides the Entity Layer Role object
    ''' </summary>
    ''' <history>
    '''     [cnurse]    05/23/2005  made compatible with .NET 2.0
    '''     [cnurse]    01/03/2006  added RoleGroupId property
    ''' </history>
    ''' -----------------------------------------------------------------------------
    <XmlRoot("role", IsNullable:=False)> Public Class RoleInfo
        Private _RoleID As Integer
        Private _PortalID As Integer
        Private _RoleGroupID As Integer
        Private _RoleName As String
        Private _Description As String
        Private _ServiceFee As Single
        Private _BillingFrequency As String
        Private _TrialPeriod As Integer
        Private _TrialFrequency As String
        Private _BillingPeriod As Integer
        Private _TrialFee As Single
        Private _IsPublic As Boolean
        Private _AutoAssignment As Boolean
        Private _RSVPCode As String
        Private _IconFile As String

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Role Id
        ''' </summary>
        ''' <value>An Integer representing the Id of the Role</value>
        ''' -----------------------------------------------------------------------------
        <XmlIgnore()> Public Property RoleID() As Integer
            Get
                Return _RoleID
            End Get
            Set(ByVal Value As Integer)
                _RoleID = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Portal Id for the Role
        ''' </summary>
        ''' <value>An Integer representing the Id of the Portal</value>
        ''' -----------------------------------------------------------------------------
        <XmlIgnore()> Public Property PortalID() As Integer
            Get
                Return _PortalID
            End Get
            Set(ByVal Value As Integer)
                _PortalID = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the RoleGroup Id
        ''' </summary>
        ''' <value>An Integer representing the Id of the RoleGroup</value>
        ''' -----------------------------------------------------------------------------
        <XmlIgnore()> Public Property RoleGroupID() As Integer
            Get
                Return _RoleGroupID
            End Get
            Set(ByVal Value As Integer)
                _RoleGroupID = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Role Name
        ''' </summary>
        ''' <value>A string representing the name of the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("rolename")> Public Property RoleName() As String
            Get
                Return _RoleName
            End Get
            Set(ByVal Value As String)
                _RoleName = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets an sets the Description of the Role
        ''' </summary>
        ''' <value>A string representing the description of the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("description")> Public Property Description() As String
            Get
                Return _Description
            End Get
            Set(ByVal Value As String)
                _Description = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Billing Frequency for the role
        ''' </summary>
        ''' <value>A String representing the Billing Frequency of the Role<br/>
        ''' <ul>
        ''' <list>N - None</list>
        ''' <list>O - One time fee</list>
        ''' <list>D - Daily</list>
        ''' <list>W - Weekly</list>
        ''' <list>M - Monthly</list>
        ''' <list>Y - Yearly</list>
        ''' </ul>
        ''' </value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("billingfrequency")> Public Property BillingFrequency() As String
            Get
                Return _BillingFrequency
            End Get
            Set(ByVal Value As String)
                _BillingFrequency = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the fee for the role
        ''' </summary>
        ''' <value>A single number representing the fee for the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("servicefee")> Public Property ServiceFee() As Single
            Get
                Return _ServiceFee
            End Get
            Set(ByVal Value As Single)
                _ServiceFee = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Trial Frequency for the role
        ''' </summary>
        ''' <value>A String representing the Trial Frequency of the Role<br/>
        ''' <ul>
        ''' <list>N - None</list>
        ''' <list>O - One time fee</list>
        ''' <list>D - Daily</list>
        ''' <list>W - Weekly</list>
        ''' <list>M - Monthly</list>
        ''' <list>Y - Yearly</list>
        ''' </ul>
        ''' </value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("trialfrequency")> Public Property TrialFrequency() As String
            Get
                Return _TrialFrequency
            End Get
            Set(ByVal Value As String)
                _TrialFrequency = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the length of the trial period
        ''' </summary>
        ''' <value>An integer representing the length of the trial period</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("trialperiod")> Public Property TrialPeriod() As Integer
            Get
                Return _TrialPeriod
            End Get
            Set(ByVal Value As Integer)
                _TrialPeriod = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the length of the billing period
        ''' </summary>
        ''' <value>An integer representing the length of the billing period</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("billingperiod")> Public Property BillingPeriod() As Integer
            Get
                Return _BillingPeriod
            End Get
            Set(ByVal Value As Integer)
                _BillingPeriod = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the trial fee for the role
        ''' </summary>
        ''' <value>A single number representing the trial fee for the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("trialfee")> Public Property TrialFee() As Single
            Get
                Return _TrialFee
            End Get
            Set(ByVal Value As Single)
                _TrialFee = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets whether the role is public
        ''' </summary>
        ''' <value>A boolean (True/False)</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("ispublic")> Public Property IsPublic() As Boolean
            Get
                Return _IsPublic
            End Get
            Set(ByVal Value As Boolean)
                _IsPublic = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets whether users are automatically assigned to the role
        ''' </summary>
        ''' <value>A boolean (True/False)</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("autoassignment")> Public Property AutoAssignment() As Boolean
            Get
                Return _AutoAssignment
            End Get
            Set(ByVal Value As Boolean)
                _AutoAssignment = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the RSVP Code for the role
        ''' </summary>
        ''' <value>A string representing the RSVP Code for the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("rsvpcode")> Public Property RSVPCode() As String
            Get
                Return _RSVPCode
            End Get
            Set(ByVal Value As String)
                _RSVPCode = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets and sets the Icon File for the role
        ''' </summary>
        ''' <value>A string representing the Icon File for the role</value>
        ''' -----------------------------------------------------------------------------
        <XmlElement("iconfile")> Public Property IconFile() As String
            Get
                Return _IconFile
            End Get
            Set(ByVal Value As String)
                _IconFile = Value
            End Set
        End Property

    End Class

End Namespace
