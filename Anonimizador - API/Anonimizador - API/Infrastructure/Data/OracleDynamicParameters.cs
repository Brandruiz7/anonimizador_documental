using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace Anonimizador___API.Infrastructure.Data
{
    /// <summary>
    /// Clase auxiliar que permite a Dapper trabajar con parámetros nativos de Oracle.
    /// Implementa <see cref="SqlMapper.IDynamicParameters"/> para inyectar
    /// <see cref="OracleParameter"/> directamente en los comandos de Dapper.
    ///
    /// Diferencias clave con SQL Server:
    /// - Los procedimientos almacenados retornan filas mediante SYS_REFCURSOR (no SELECT directo)
    /// - Los valores de salida escalares requieren parámetros OUT explícitos
    /// - Todos los parámetros deben tiparse con <see cref="OracleDbType"/>
    ///
    /// Helpers disponibles:
    /// - <see cref="AddInput"/>  → parámetros de entrada
    /// - <see cref="AddOutput"/> → parámetros de salida escalares (ej. IDs generados)
    /// - <see cref="AddCursor"/> → cursores de salida para conjuntos de filas
    /// - <see cref="Get{T}"/>    → recuperar valores de salida después de ejecutar el SP
    /// </summary>
    public class OracleDynamicParameters : SqlMapper.IDynamicParameters
    {
        private readonly List<OracleParameter> _parameters = new();

        /// <summary>
        /// Agrega un parámetro Oracle con control completo sobre tipo, dirección y tamaño.
        /// Para la mayoría de casos usar los helpers <see cref="AddInput"/>,
        /// <see cref="AddOutput"/> o <see cref="AddCursor"/> en su lugar.
        /// </summary>
        /// <param name="name">Nombre del parámetro tal como aparece en el SP (ej. p_FileName).</param>
        /// <param name="value">Valor a enviar. Si es null se envía DBNull.</param>
        /// <param name="dbType">Tipo de dato Oracle. Por defecto NVarchar2.</param>
        /// <param name="direction">Dirección del parámetro: Input, Output o InputOutput.</param>
        /// <param name="size">Tamaño opcional para tipos como NVarchar2.</param>
        public void Add(
            string name,
            object? value = null,
            OracleDbType dbType = OracleDbType.NVarchar2,
            ParameterDirection direction = ParameterDirection.Input,
            int size = 0)
        {
            var param = new OracleParameter
            {
                ParameterName = name,
                OracleDbType = dbType,
                Direction = direction,
                Value = value ?? DBNull.Value
            };

            if (size > 0) param.Size = size;

            _parameters.Add(param);
        }

        /// <summary>
        /// Agrega un parámetro de entrada (Input). Forma abreviada de <see cref="Add"/>.
        /// Usar para todos los valores que se envían al SP.
        /// Por defecto el tipo es NVarchar2 — especificar otro tipo si es numérico o fecha.
        /// </summary>
        /// <param name="name">Nombre del parámetro en el SP.</param>
        /// <param name="value">Valor a enviar.</param>
        /// <param name="dbType">Tipo Oracle. Por defecto NVarchar2.</param>
        public void AddInput(
            string name,
            object? value,
            OracleDbType dbType = OracleDbType.NVarchar2)
            => Add(name, value, dbType, ParameterDirection.Input);

        /// <summary>
        /// Agrega un parámetro de salida (Output) para valores escalares.
        /// Usar cuando el SP retorna un valor único como un ID generado.
        /// Recuperar el valor después de ejecutar con <see cref="Get{T}"/>.
        /// </summary>
        /// <param name="name">Nombre del parámetro en el SP.</param>
        /// <param name="dbType">Tipo Oracle del valor de salida.</param>
        public void AddOutput(string name, OracleDbType dbType)
            => Add(name, null, dbType, ParameterDirection.Output);

        /// <summary>
        /// Agrega un cursor de salida (SYS_REFCURSOR).
        /// Usar cuando el SP retorna un conjunto de filas.
        /// En Oracle los SPs no retornan filas directamente como en SQL Server —
        /// se declara un cursor OUT y Dapper lo lee automáticamente como un result set.
        /// </summary>
        /// <param name="name">Nombre del parámetro cursor en el SP (ej. p_ResultSet).</param>
        public void AddCursor(string name)
            => Add(name, null, OracleDbType.RefCursor, ParameterDirection.Output);

        /// <summary>
        /// Recupera el valor de un parámetro de salida después de ejecutar el SP.
        /// Maneja la conversión desde tipos nativos de Oracle hacia tipos .NET estándar.
        /// Oracle no retorna tipos primitivos directamente — retorna OracleDecimal, OracleString, etc.
        /// </summary>
        /// <typeparam name="T">Tipo .NET al que se convierte el valor retornado.</typeparam>
        /// <param name="name">Nombre del parámetro de salida.</param>
        /// <exception cref="InvalidOperationException">Si el parámetro no existe.</exception>
        public T Get<T>(string name)
        {
            var param = _parameters.First(p => p.ParameterName == name);
            var value = param.Value;

            // Oracle retorna sus propios tipos en lugar de tipos .NET primitivos
            // OracleDecimal → int/long/decimal, OracleString → string
            if (value is OracleDecimal oracleDecimal)
                value = (int)oracleDecimal.Value;
            else if (value is OracleString oracleString)
                value = oracleString.Value;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Método requerido por Dapper para inyectar los parámetros Oracle
        /// en el comando antes de ejecutarlo. No invocar directamente —
        /// Dapper lo llama internamente al ejecutar el SP.
        /// </summary>
        void SqlMapper.IDynamicParameters.AddParameters(
            IDbCommand command, SqlMapper.Identity identity)
        {
            foreach (var p in _parameters)
                command.Parameters.Add(p);
        }
    }
}