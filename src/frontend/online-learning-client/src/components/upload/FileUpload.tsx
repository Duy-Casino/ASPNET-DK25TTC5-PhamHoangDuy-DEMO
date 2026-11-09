import { useId, useRef, useState } from 'react'
import { Upload } from 'lucide-react'
import { toast } from 'sonner'
import { getErrorMessage } from '../../services/apiClient'
import { uploadService, type UploadResult } from '../../services/paymentApi'

const accepts: Record<UploadResult['kind'], { accept: string; hint: string; maxMb: number }> = {
  image: { accept: 'image/jpeg,image/png,image/webp,image/gif', hint: 'JPG, PNG, WEBP, GIF · tối đa 5 MB', maxMb: 5 },
  video: { accept: 'video/mp4,video/webm,video/quicktime,.m4v', hint: 'MP4, WEBM, MOV · tối đa 500 MB', maxMb: 500 },
  document: { accept: '.pdf,.zip,.rar,.docx,.xlsx,.pptx,.txt,.md', hint: 'PDF, ZIP, DOCX, XLSX, PPTX, TXT · tối đa 50 MB', maxMb: 50 },
}

/**
 * Nút tải tệp lên kèm thanh tiến độ. Kiểm tra dung lượng ngay ở trình duyệt;
 * Backend kiểm tra lại định dạng, dung lượng và chữ ký tệp.
 */
export function FileUpload({ kind, label, onUploaded, disabled }: {
  kind: UploadResult['kind']
  label: string
  onUploaded: (result: UploadResult) => void
  disabled?: boolean
}) {
  const inputId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [progress, setProgress] = useState<number | null>(null)
  const cfg = accepts[kind]

  const handle = async (file: File | undefined) => {
    if (!file) return
    if (file.size > cfg.maxMb * 1024 * 1024) {
      toast.error(`Tệp vượt quá ${cfg.maxMb} MB.`)
      return
    }
    setProgress(0)
    try {
      const result = await uploadService.upload(kind, file, setProgress)
      onUploaded(result)
      toast.success(`Đã tải lên "${result.fileName}"`)
    } catch (e) {
      toast.error(getErrorMessage(e, 'Tải tệp lên thất bại.'))
    } finally {
      setProgress(null)
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  const uploading = progress !== null
  return (
    <div className="flex flex-col gap-1.5">
      <input id={inputId} ref={inputRef} type="file" accept={cfg.accept} className="sr-only" disabled={disabled || uploading}
        onChange={(e) => void handle(e.target.files?.[0])} aria-describedby={`${inputId}-hint`} />
      <label htmlFor={inputId}
        className={`btn-soft w-fit cursor-pointer ${disabled || uploading ? 'pointer-events-none opacity-60' : ''}`}>
        <Upload className="size-4" aria-hidden="true" />
        {uploading ? `Đang tải lên… ${progress}%` : label}
      </label>
      {uploading && (
        <span className="h-1.5 w-full max-w-xs overflow-hidden rounded-full bg-surface-3" role="progressbar" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100} aria-label="Tiến độ tải lên">
          <span className="block h-full bg-brand-600 transition-[width]" style={{ width: `${progress}%` }} />
        </span>
      )}
      <p id={`${inputId}-hint`} className="text-xs text-ink-muted">{cfg.hint}</p>
    </div>
  )
}
