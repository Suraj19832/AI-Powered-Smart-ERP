import type { Field } from '../../components/FormDialog'

export const customerFields: Field[] = [
  { name: 'name', label: 'Customer name', required: true, span: 2 },
  { name: 'email', label: 'Email', type: 'email' },
  { name: 'phone', label: 'Phone' },
  { name: 'address', label: 'Address', span: 2 },
  { name: 'city', label: 'City' },
  { name: 'taxNumber', label: 'GSTIN / Tax no.' },
  { name: 'creditLimit', label: 'Credit limit', type: 'number', min: 0 },
  { name: 'isActive', label: 'Active', type: 'switch' },
]

export const supplierFields: Field[] = [
  { name: 'name', label: 'Supplier name', required: true, span: 2 },
  { name: 'contactPerson', label: 'Contact person' },
  { name: 'email', label: 'Email', type: 'email' },
  { name: 'phone', label: 'Phone' },
  { name: 'city', label: 'City' },
  { name: 'address', label: 'Address', span: 2 },
  { name: 'taxNumber', label: 'GSTIN / Tax no.' },
  { name: 'leadTimeDays', label: 'Lead time (days)', type: 'number', min: 0, max: 365, helperText: 'Used by smart reorder' },
  { name: 'isActive', label: 'Active', type: 'switch' },
]
