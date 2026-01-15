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
Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection
Imports System.Text
Imports System.Web.Caching
Imports System.Xml
Imports System.Xml.Serialization

Imports DotNetNuke.Entities.Modules

Namespace DotNetNuke.Common.Utilities

    Public Class CBO

#Region "Private Shared Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CreateObject creates an object of a specified type from the provided DataReader
        ''' </summary>
        ''' <param name="objType">The type of the business object</param>
        ''' <param name="dr">The DataReader</param>
        ''' <returns>The custom business object</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	06/29/2007	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function CreateObject(ByVal objType As Type, ByVal dr As IDataReader) As Object
            Dim objObject As Object = Activator.CreateInstance(objType)

            If TypeOf objObject Is IHydratable Then
                'Create Object and Use IHydratable's Fill
                Dim objHydratable As IHydratable = TryCast(objObject, IHydratable)
                If objHydratable IsNot Nothing Then
                    objHydratable.Fill(dr)
                End If
            Else
                'Use Reflection
                HydrateObject(objObject, dr)
            End If

            Return objObject
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of CreateObject creates an object of a specified type from the 
        ''' provided DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object</typeparam>
        ''' <param name="dr">The DataReader</param>
        ''' <returns>The custom business object</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function CreateObject(Of T)(ByVal dr As IDataReader) As T
            Dim objObject As T = Activator.CreateInstance(Of T)()

            If TypeOf objObject Is IHydratable Then
                'Use IHydratable's Fill
                Dim objHydratable As IHydratable = TryCast(objObject, IHydratable)
                If objHydratable IsNot Nothing Then
                    objHydratable.Fill(dr)
                End If
            Else
                'Use Reflection
                HydrateObject(objObject, dr)
            End If

            Return objObject
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetOrdinals gets the array of Ordinals in the dataReader that corresponds to the 
        ''' ArrayList of properties in the class
        ''' </summary>
        ''' <param name="dr">The DataReader</param>
        ''' <param name="objProperties">An ArrayList of properties for the type</param>
        ''' <returns>An Array of ordinals</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	06/29/2007	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Function GetOrdinals(ByVal objProperties As ArrayList, ByVal dr As IDataReader) As Integer()
            Dim arrOrdinals(objProperties.Count) As Integer
            Dim propertyName As String

            Dim intIndex As Integer
            Dim columns As New Hashtable

            If Not dr Is Nothing Then
                'Get the column names from the DataReader
                For intIndex = 0 To dr.FieldCount - 1
                    columns(dr.GetName(intIndex).ToUpperInvariant()) = ""
                Next intIndex

                For intIndex = 0 To objProperties.Count - 1
                    propertyName = DirectCast(objProperties(intIndex), PropertyInfo).Name.ToUpperInvariant()
                    If (columns.ContainsKey(propertyName)) Then
                        arrOrdinals(intIndex) = dr.GetOrdinal(propertyName)
                    Else
                        arrOrdinals(intIndex) = -1
                    End If
                Next
            End If

            Return arrOrdinals
        End Function

        Private Shared Sub HydrateObject(ByVal objObject As Object, ByVal dr As IDataReader)
            Dim objPropertyInfo As PropertyInfo
            Dim objPropertyType As Type = Nothing
            Dim objDataValue As Object
            Dim objDataType As Type
            Dim intProperty As Integer

            ' get properties for type
            Dim objProperties As ArrayList = GetPropertyInfo(objObject.GetType)

            ' get ordinal positions in datareader
            Dim arrOrdinals As Integer() = GetOrdinals(objProperties, dr)

            ' fill object with values from datareader
            For intProperty = 0 To objProperties.Count - 1
                'Get the property to set
                objPropertyInfo = CType(objProperties(intProperty), PropertyInfo)

                'Get its type
                objPropertyType = objPropertyInfo.PropertyType

                'If property can be set
                If objPropertyInfo.CanWrite Then

                    'If the DataReader contains the column that matches the Properties name
                    If arrOrdinals(intProperty) <> -1 Then
                        'Get the Data Value from the data reader
                        objDataValue = dr.GetValue(arrOrdinals(intProperty))

                        'Get the Dat Value's type
                        objDataType = objDataValue.GetType

                        If IsDBNull(objDataValue) Then
                            ' set property value to Null
                            objPropertyInfo.SetValue(objObject, Null.SetNull(objPropertyInfo), Nothing)
                        ElseIf objPropertyType.Equals(objDataType) Then
                            'Property and data objects are the same type
                            objPropertyInfo.SetValue(objObject, objDataValue, Nothing)
                        Else
                            ' business object info class member data type does not match datareader member data type
                            Try
                                'need to handle enumeration conversions differently than other base types
                                If objPropertyType.BaseType.Equals(GetType(System.Enum)) Then
                                    ' check if value is numeric and if not convert to integer ( supports databases like Oracle )
                                    If IsNumeric(objDataValue) Then
                                        objPropertyInfo.SetValue(objObject, System.Enum.ToObject(objPropertyType, Convert.ToInt32(objDataValue)), Nothing)
                                    Else
                                        objPropertyInfo.SetValue(objObject, System.Enum.ToObject(objPropertyType, objDataValue), Nothing)
                                    End If
                                ElseIf objPropertyType.FullName.Equals("System.Guid") Then
                                    ' guid is not a datatype common across all databases ( ie. Oracle )
                                    objPropertyInfo.SetValue(objObject, Convert.ChangeType(New Guid(objDataValue.ToString()), objPropertyType), Nothing)
                                Else
                                    ' try explicit conversion
                                    'objPropertyInfo.SetValue(objObject, Convert.ChangeType(objDataValue, objPropertyType), Nothing)
                                    objPropertyInfo.SetValue(objObject, objDataValue, Nothing)
                                End If
                            Catch
                                objPropertyInfo.SetValue(objObject, Convert.ChangeType(objDataValue, objPropertyType), Nothing)
                            End Try
                        End If
                    Else
                        ' property does not exist in datareader
                    End If
                End If
            Next intProperty
        End Sub

#End Region

#Region "Public Shared Methods"

        Public Shared Function CloneObject(ByVal ObjectToClone As Object) As Object    'Implements System.ICloneable.Clone
            Try
                Dim newObject As Object = DotNetNuke.Framework.Reflection.CreateObject(ObjectToClone.GetType().AssemblyQualifiedName, ObjectToClone.GetType().AssemblyQualifiedName)

                Dim props As ArrayList = GetPropertyInfo(newObject.GetType())

                Dim i As Integer = 0

                For i = 0 To GetPropertyInfo(ObjectToClone.GetType()).Count - 1
                    Dim p As PropertyInfo = CType(GetPropertyInfo(ObjectToClone.GetType())(i), PropertyInfo)

                    Dim ICloneType As Type = p.PropertyType.GetInterface("ICloneable", True)
                    If CType(props(i), PropertyInfo).CanWrite Then
                        If Not (ICloneType Is Nothing) Then
                            Dim IClone As ICloneable = CType(p.GetValue(ObjectToClone, Nothing), ICloneable)
                            CType(props(i), PropertyInfo).SetValue(newObject, IClone.Clone(), Nothing)
                        Else
                            CType(props(i), PropertyInfo).SetValue(newObject, p.GetValue(ObjectToClone, Nothing), Nothing)
                        End If

                        Dim IEnumerableType As Type = p.PropertyType.GetInterface("IEnumerable", True)
                        If Not (IEnumerableType Is Nothing) Then
                            Dim IEnum As IEnumerable = CType(p.GetValue(ObjectToClone, Nothing), IEnumerable)

                            Dim IListType As Type = CType(props(i), PropertyInfo).PropertyType.GetInterface("IList", True)
                            Dim IDicType As Type = CType(props(i), PropertyInfo).PropertyType.GetInterface("IDictionary", True)

                            Dim j As Integer = 0
                            If Not (IListType Is Nothing) Then
                                Dim list As IList = CType(CType(props(i), PropertyInfo).GetValue(newObject, Nothing), IList)

                                Dim obj As Object
                                For Each obj In IEnum
                                    ICloneType = obj.GetType().GetInterface("ICloneable", True)

                                    If Not (ICloneType Is Nothing) Then
                                        Dim tmpClone As ICloneable = CType(obj, ICloneable)
                                        list(j) = tmpClone.Clone()
                                    End If

                                    j += 1
                                Next obj
                            Else
                                If Not (IDicType Is Nothing) Then
                                    Dim dic As IDictionary = CType(CType(props(i), PropertyInfo).GetValue(newObject, Nothing), IDictionary)
                                    j = 0

                                    Dim de As DictionaryEntry
                                    For Each de In IEnum

                                        ICloneType = de.Value.GetType().GetInterface("ICloneable", True)

                                        If Not (ICloneType Is Nothing) Then
                                            Dim tmpClone As ICloneable = CType(de.Value, ICloneable)
                                            dic(de.Key) = tmpClone.Clone()
                                        End If
                                        j += 1
                                    Next de
                                End If
                            End If
                        End If
                    End If
                Next
                Return newObject
            Catch exc As Exception
                LogException(exc)
                Return Nothing
            End Try
        End Function

        Public Shared Function FillCollection(ByVal dr As IDataReader, ByVal objType As Type) As ArrayList

            Dim objFillCollection As New ArrayList
            Dim objFillObject As Object

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(objType, dr)
                ' add to collection
                objFillCollection.Add(objFillObject)
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objFillCollection

        End Function

        Public Shared Function FillCollection(ByVal dr As IDataReader, ByVal objType As Type, ByRef objToFill As IList) As IList

            Dim objFillObject As Object

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(objType, dr)
                ' add to collection
                objToFill.Add(objFillObject)
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objToFill

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillCollection fills a List custom business object of a specified type 
        ''' from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <returns>A List of custom business objects</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillCollection(Of T)(ByVal dr As IDataReader) As List(Of T)

            Dim objFillCollection As New List(Of T)
            Dim objFillObject As T

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(Of T)(dr)
                ' add to collection
                objFillCollection.Add(objFillObject)
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objFillCollection

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillCollection fills a provided IList with custom business 
        ''' objects of a specified type from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <param name="objToFill">The IList to fill</param>
        ''' <returns>An IList of custom business objects</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillCollection(Of T)(ByVal dr As IDataReader, ByRef objToFill As IList(Of T)) As IList(Of T)

            Dim objFillObject As T

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(Of T)(dr)
                ' add to collection
                objToFill.Add(objFillObject)
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objToFill

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillCollection fills a List custom business object of a specified type 
        ''' from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <returns>A List of custom business objects</returns>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillCollection(Of T)(ByVal dr As IDataReader, ByRef totalRecords As Integer) As List(Of T)

            Dim objFillCollection As New List(Of T)
            Dim objFillObject As T

            Try
                ' iterate first datareader to fill collection
                While dr.Read
                    ' fill business object
                    objFillObject = CreateObject(Of T)(dr)
                    ' add to collection
                    objFillCollection.Add(objFillObject)
                End While

                If dr.NextResult Then
                    'Get the total no of records from the second result
                    totalRecords = Globals.GetTotalRecords(dr)
                End If
            Catch exc As Exception
                Exceptions.LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return objFillCollection

        End Function

        Public Shared Function FillCollection(ByVal dr As IDataReader, ByVal objType As Type, ByVal ManageDataReader As Boolean) As ArrayList

            Dim objFillCollection As New ArrayList
            Dim objFillObject As Object

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(objType, dr)
                ' add to collection
                objFillCollection.Add(objFillObject)
            End While

            If ManageDataReader Then
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End If

            Return objFillCollection

        End Function

        Public Shared Function FillCollection(ByVal dr As IDataReader, ByVal objType As Type, ByRef objToFill As IList, ByVal ManageDataReader As Boolean) As IList

            Dim objFillObject As Object

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(objType, dr)
                ' add to collection
                objToFill.Add(objFillObject)
            End While

            If ManageDataReader Then
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End If

            Return objToFill

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' FillDictionary fills an IDictionary with custom business 
        ''' objects of a specified type from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object.  Note: T must implement IHydratable</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <returns>An IDictionary of custom business objects</returns>
        ''' <remarks>The key for the Dictionary is the ItemID property of the IHydratable interface.</remarks>
        ''' <history>
        ''' 	[cnurse]	07/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillDictionary(Of T As IHydratable)(ByVal dr As IDataReader) As IDictionary(Of Integer, T)

            Dim objFillDictionary As New Dictionary(Of Integer, T)
            Dim objFillObject As T

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(Of T)(dr)

                ' add to dictionary
                If objFillObject IsNot Nothing Then
                    objFillDictionary.Add(objFillObject.KeyID, objFillObject)
                End If
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objFillDictionary
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillDictionary fills a provided IDictionary with custom business 
        ''' objects of a specified type from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the business object.  Note: T must implement IHydratable</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <param name="objToFill">The IDictionary to fill</param>
        ''' <returns>An IDictionary of custom business objects</returns>
        ''' <remarks>The key for the Dictionary is the ItemID property of the IHydratable interface.</remarks>
        ''' <history>
        ''' 	[cnurse]	07/04/2007	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillDictionary(Of T As IHydratable)(ByVal dr As IDataReader, ByRef objToFill As IDictionary(Of Integer, T)) As IDictionary(Of Integer, T)

            Dim objFillObject As T

            ' iterate datareader
            While dr.Read
                ' fill business object
                objFillObject = CreateObject(Of T)(dr)

                ' add to dictionary
                If objFillObject IsNot Nothing Then
                    objToFill.Add(objFillObject.KeyID, objFillObject)
                End If
            End While

            ' close datareader
            If Not dr Is Nothing Then
                dr.Close()
            End If

            Return objToFill

        End Function

        Public Shared Function FillObject(ByVal dr As IDataReader, ByVal objType As Type) As Object
            Return FillObject(dr, objType, True)
        End Function

        Public Shared Function FillObject(ByVal dr As IDataReader, ByVal objType As Type, ByVal ManageDataReader As Boolean) As Object

            Dim objFillObject As Object

            Dim [Continue] As Boolean
            If ManageDataReader Then
                [Continue] = False
                ' read datareader
                If dr.Read() Then
                    [Continue] = True
                End If
            Else
                [Continue] = True
            End If

            If [Continue] Then
                ' create custom business object
                objFillObject = CreateObject(objType, dr)
            Else
                objFillObject = Nothing
            End If

            If ManageDataReader Then
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End If

            Return objFillObject

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillObject fills a custom business object of a specified type 
        ''' from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the object</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <returns>The object</returns>
        ''' <remarks>This overloads sets the ManageDataReader parameter to true and calls 
        ''' the other overload</remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillObject(Of T)(ByVal dr As IDataReader) As T
            Return FillObject(Of T)(dr, True)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Generic version of FillObject fills a custom business object of a specified type 
        ''' from the supplied DataReader
        ''' </summary>
        ''' <typeparam name="T">The type of the object</typeparam>
        ''' <param name="dr">The IDataReader to use to fill the object</param>
        ''' <param name="ManageDataReader">A boolean that determines whether the DatReader
        ''' is managed</param>
        ''' <returns>The object</returns>
        ''' <remarks>This overloads allows the caller to determine whether the ManageDataReader 
        ''' parameter is set</remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2005	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function FillObject(Of T)(ByVal dr As IDataReader, ByVal ManageDataReader As Boolean) As T

            Dim objFillObject As T

            Dim [Continue] As Boolean
            If ManageDataReader Then
                [Continue] = False
                ' read datareader
                If dr.Read() Then
                    [Continue] = True
                End If
            Else
                [Continue] = True
            End If

            If [Continue] Then
                ' create custom business object
                objFillObject = CreateObject(Of T)(dr)
            Else
                objFillObject = Nothing
            End If

            If ManageDataReader Then
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End If

            Return objFillObject

        End Function

        Public Shared Function GetPropertyInfo(ByVal objType As Type) As ArrayList

            ' Use the cache because the reflection used later is expensive
            Dim objProperties As ArrayList = CType(DataCache.GetCache(objType.FullName), ArrayList)

            If objProperties Is Nothing Then
                objProperties = New ArrayList
                Dim objProperty As PropertyInfo
                For Each objProperty In objType.GetProperties()
                    objProperties.Add(objProperty)
                Next
                DataCache.SetCache(objType.FullName, objProperties)
            End If

            Return objProperties

        End Function

        Public Shared Function InitializeObject(ByVal objObject As Object, ByVal objType As Type) As Object

            Dim objPropertyInfo As PropertyInfo
            Dim objValue As Object
            Dim intProperty As Integer

            ' get properties for type
            Dim objProperties As ArrayList = GetPropertyInfo(objType)

            ' initialize properties
            For intProperty = 0 To objProperties.Count - 1
                objPropertyInfo = CType(objProperties(intProperty), PropertyInfo)
                If objPropertyInfo.CanWrite Then
                    objValue = Null.SetNull(objPropertyInfo)
                    objPropertyInfo.SetValue(objObject, objValue, Nothing)
                End If
            Next intProperty

            Return objObject

        End Function

        Public Shared Function Serialize(ByVal objObject As Object) As XmlDocument

            Dim objXmlSerializer As New XmlSerializer(objObject.GetType())

            Dim objStringBuilder As New StringBuilder

            Dim objTextWriter As TextWriter = New StringWriter(objStringBuilder)

            objXmlSerializer.Serialize(objTextWriter, objObject)

            Dim objStringReader As New StringReader(objTextWriter.ToString())

            Dim objDataSet As New DataSet

            objDataSet.ReadXml(objStringReader)

            Dim xmlSerializedObject As New XmlDocument

            xmlSerializedObject.LoadXml(objDataSet.GetXml())

            Return xmlSerializedObject

        End Function

#End Region

    End Class


End Namespace