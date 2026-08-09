from fastapi import status

class AppError(Exception):
	status_code: int = status.HTTP_500_INTERNAL_SERVER_ERROR
	detail: str = "Unexpected Error."

	def __init__(self, detail: str | None = None) -> None:
		self.detail = detail or self.__class__.detail
		super().__init__(self.detail)

class ValidationError(AppError):
	status_code = status.HTTP_400_BAD_REQUEST
	detail = "Invalid request data."

class NotFoundError(AppError):
	status_code = status.HTTP_404_NOT_FOUND
	detail = "Resource not found."

class AiProviderError(AppError):
	status_code = status.HTTP_502_BAD_GATEWAY
	detail = "AI provider request failed."

class DataCorruptionError(AppError):
	status_code = status.HTTP_500_INTERNAL_SERVER_ERROR
	detail = "Stored data is invalid."