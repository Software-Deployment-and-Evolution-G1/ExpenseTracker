const deleteExpenseModal = document.getElementById('deleteExpenseModal');

if (deleteExpenseModal) {
    deleteExpenseModal.addEventListener('show.bs.modal', event => {
        const trigger = event.relatedTarget;
        document.getElementById('deleteExpenseId').value = trigger.dataset.expenseId;
        document.getElementById('deleteExpenseDetails').textContent =
            `${trigger.dataset.expenseDescription} — ${trigger.dataset.expenseDate} — ${trigger.dataset.expenseAmount}`;
    });

    deleteExpenseModal.addEventListener('shown.bs.modal', () => {
        document.getElementById('cancelDeleteExpense').focus();
    });
}
