/**
 * Agrega una variación a un campo específico de una persona.
 * param {number} personIndex - Índice de la persona
 * param {string} fieldName   - Nombre del campo (NameVariations, IdVariations, PhoneVariations)
 * param {string} label       - Etiqueta visible para el usuario
 * param {string} placeholder - Texto de ayuda del input
 */
function addFieldVariation(personIndex, fieldName, label, placeholder) {
    const containerId = `variations-${personIndex}-${fieldName}`;
    const varId = `${containerId}-${Date.now()}`;
    const container = document.getElementById(containerId);

    if (!container) return;

    const currentCount = container.querySelectorAll('input').length;

    container.insertAdjacentHTML('beforeend', `
        <div class="detected-item" id="${varId}" style="margin-top:6px;">
            <span class="detected-item-type"
                  style="color:var(--accent-teal); font-size:11px;">
                ${label}
            </span>
            <input type="text"
                   class="detected-item-input"
                   name="Persons[${personIndex}].${fieldName}[${currentCount}]"
                   data-person="${personIndex}"
                   data-field="${fieldName}"
                   data-variation="true"
                   placeholder="${placeholder}" />
            <button type="button"
                    class="btn-remove-detected"
                    onclick="removeDetectedItem('${varId}')"
                    title="Eliminar variación">✕</button>
        </div>`);

    document.getElementById(varId).querySelector('input').focus();
}