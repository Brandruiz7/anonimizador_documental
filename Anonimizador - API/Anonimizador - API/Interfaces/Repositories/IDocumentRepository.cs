namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Define el contrato para el acceso a datos de documentos y sus versiones.
    /// 
    /// Esta interfaz abstrae la comunicación con la base de datos, permitiendo:
    /// - Insertar documentos
    /// - Registrar versiones (originales y anonimizadas)
    /// - Validar duplicados mediante hash
    /// 
    /// Su implementación concreta se encuentra en la capa Infrastructure.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Registra un proceso de anonimización.
        /// </summary>
        /// <param name="fileName">Nombre original.</param>
        /// <param name="contentType">Tipo MIME.</param>
        /// <param name="fileSizeKb">Tamaño en KB.</param>
        /// <param name="hash">Hash SHA256.</param>
        /// <param name="uploadedBy">Usuario.</param>
        /// <returns>ID generado.</returns>
        Task<int> InsertDocumentProcessAsync(string fileName,string contentType, long fileSizeKb, string hash, string uploadedBy);

        /// <summary>
        /// Inserta una nueva versión asociada a un documento existente.
        /// 
        /// Un documento puede tener múltiples versiones:
        /// - ORIGINAL (archivo tal cual se sube)
        /// - ANONYMIZED (archivo procesado sin datos sensibles)
        /// 
        /// Este método registra la ruta física del archivo y su hash correspondiente.
        /// </summary>
        /// <param name="documentId">Identificador del documento al que pertenece la versión.</param>
        /// <param name="versionType">Tipo de versión (ej: ORIGINAL, ANONYMIZED).</param>
        /// <param name="path">Ruta física donde se encuentra almacenado el archivo.</param>
        /// <param name="hash">Hash del archivo correspondiente a esta versión.</param>
        /// <returns>
        /// Retorna el identificador de la versión creada en la base de datos.
        /// </returns>
        Task<int> InsertVersionAsync(int documentId, string versionType, string path, string hash);

        /// <summary>
        /// Obtiene el identificador de un documento existente a partir de su hash.
        /// 
        /// Este método se utiliza para evitar duplicados:
        /// si ya existe un documento con el mismo hash, no se inserta uno nuevo.
        /// </summary>
        /// <param name="hash">Hash SHA256 del archivo.</param>
        /// <returns>
        /// Retorna el DocumentId si el documento existe; en caso contrario, retorna null.
        /// </returns>
        Task<int?> GetDocumentByHashAsync(string hash);

        /// <summary>
        /// Actualiza el estado del procesamiento.
        /// </summary>
        /// <param name="documentId">
        /// Identificador del documento.
        /// </param>
        /// <param name="statusId">
        /// Identificador del estado.
        /// </param>
        Task UpdateProcessStatusAsync( int documentId, int statusId);

        /// <summary>
        /// Registra una versión del documento (ORIGINAL o ANONYMIZED).
        /// Retorna el VersionId generado.
        /// </summary>
        Task<int> InsertDocumentVersionAsync(int documentId, string versionType, string fileHash);

        /// <summary>
        /// Registra un campo anonimizado para auditoría.
        /// </summary>
        Task InsertAuditFieldAsync(int versionId, string fieldType, string originalValue, string anonymizedValue);
    }
}